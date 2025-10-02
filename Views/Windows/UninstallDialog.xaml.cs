using System.Windows;
using Modrix.Services;
using Wpf.Ui.Controls;

namespace Modrix.Views.Windows
{
    public partial class UninstallDialog : FluentWindow
    {
        private readonly IUninstallService _uninstallService;
        private bool _isUninstalling = false;

        public bool ShouldExit { get; private set; } = false;

        public UninstallDialog(IUninstallService uninstallService)
        {
            _uninstallService = uninstallService;
            InitializeComponent();
            
            // Set up checkbox event handler to update the note
            DeleteProjectsCheckBox.Checked += OnDeleteProjectsChanged;
            DeleteProjectsCheckBox.Unchecked += OnDeleteProjectsChanged;
        }

        private void OnDeleteProjectsChanged(object sender, RoutedEventArgs e)
        {
            if (DeleteProjectsCheckBox.IsChecked == true)
            {
                PreservationNote.Text = "Warning: Your projects and user data will be permanently deleted!";
                PreservationNote.Foreground = System.Windows.Media.Brushes.Orange;
            }
            else
            {
                PreservationNote.Text = "Note: Your projects and user data will be preserved.";
                PreservationNote.Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorTertiaryBrush");
            }
        }

        private async void Uninstall_Click(object sender, RoutedEventArgs e)
        {
            if (_isUninstalling) return;

            // Check if running as administrator
            if (!_uninstallService.IsRunningAsAdministrator())
            {
                var result = await new Wpf.Ui.Controls.MessageBox
                {
                    Title = "Administrator Rights Required",
                    Content = "Uninstalling Modrix requires administrator privileges. Would you like to restart as administrator?",
                    PrimaryButtonText = "Restart as Admin",
                    CloseButtonText = "Cancel"
                }.ShowDialogAsync();

                if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
                {
                    var success = await _uninstallService.RequestAdministratorPrivilegesAsync();
                    if (success)
                    {
                        ShouldExit = true;
                        this.Close();
                        return;
                    }
                    else
                    {
                        await new Wpf.Ui.Controls.MessageBox
                        {
                            Title = "Error",
                            Content = "Failed to restart with administrator privileges.",
                            PrimaryButtonText = "OK"
                        }.ShowDialogAsync();
                        return;
                    }
                }
                else
                {
                    return;
                }
            }

            // Show additional confirmation if user wants to delete projects
            if (DeleteProjectsCheckBox.IsChecked == true)
            {
                var confirmResult = await new Wpf.Ui.Controls.MessageBox
                {
                    Title = "Confirm Project Deletion",
                    Content = "Are you sure you want to permanently delete all your Modrix projects and user data? This action cannot be undone.",
                    PrimaryButtonText = "Yes, Delete Everything",
                    CloseButtonText = "No, Keep Projects"
                }.ShowDialogAsync();

                if (confirmResult != Wpf.Ui.Controls.MessageBoxResult.Primary)
                {
                    return; // User cancelled
                }
            }

            _isUninstalling = true;

            // Show progress and hide buttons
            ProgressSection.Visibility = Visibility.Visible;
            ButtonsSection.Visibility = Visibility.Collapsed;

            var progress = new Progress<string>(status =>
            {
                ProgressText.Text = status;
            });

            try
            {
                var deleteProjects = DeleteProjectsCheckBox.IsChecked == true;
                var success = await _uninstallService.UninstallModrixAsync(progress, deleteProjects);
                
                if (success)
                {
                    var successMessage = deleteProjects 
                        ? "Modrix and all your projects have been successfully uninstalled from your system.\n\nThank you for using Modrix!"
                        : "Modrix has been successfully uninstalled from your system.\nYour projects have been preserved.\n\nThank you for using Modrix!";

                    await new Wpf.Ui.Controls.MessageBox
                    {
                        Title = "Uninstallation Complete",
                        Content = successMessage,
                        PrimaryButtonText = "Close"
                    }.ShowDialogAsync();
                    
                    ShouldExit = true;
                    this.Close();
                }
                else
                {
                    await new Wpf.Ui.Controls.MessageBox
                    {
                        Title = "Uninstallation Failed",
                        Content = "The uninstallation process encountered errors. Some files or registry entries may not have been removed.",
                        PrimaryButtonText = "OK"
                    }.ShowDialogAsync();
                    
                    // Reset UI
                    ProgressSection.Visibility = Visibility.Collapsed;
                    ButtonsSection.Visibility = Visibility.Visible;
                    _isUninstalling = false;
                }
            }
            catch (Exception ex)
            {
                await new Wpf.Ui.Controls.MessageBox
                {
                    Title = "Uninstallation Error",
                    Content = $"An error occurred during uninstallation:\n\n{ex.Message}",
                    PrimaryButtonText = "OK"
                }.ShowDialogAsync();
                
                // Reset UI
                ProgressSection.Visibility = Visibility.Collapsed;
                ButtonsSection.Visibility = Visibility.Visible;
                _isUninstalling = false;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (!_isUninstalling)
            {
                this.Close();
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_isUninstalling)
            {
                e.Cancel = true; // Prevent closing during uninstallation
            }
            base.OnClosing(e);
        }
    }
}