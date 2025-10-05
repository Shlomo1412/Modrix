using System.Windows;
using Modrix.Services;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;

namespace Modrix.Views.Windows;

public partial class UpdateDialog : FluentWindow
{
    private readonly UpdateInfo _updateInfo;
    private readonly IUpdateService _updateService;
    private bool _isInstalling = false;

    public bool ShouldInstall { get; private set; }
    public bool ShouldSkipVersion { get; private set; }

    public UpdateDialog(UpdateInfo updateInfo, IUpdateService updateService)
    {
        InitializeComponent();
        _updateInfo = updateInfo;
        _updateService = updateService;
        InitializeData();
    }

    private void InitializeData()
    {
        // Set update information
        UpdateVersionText.Text = $"Modrix {_updateInfo.Version} is now available!";
        CurrentVersionText.Text = _updateService.GetCurrentVersion();
        NewVersionText.Text = _updateInfo.Version;
        FileSizeText.Text = _updateInfo.FormattedSize;
        ReleaseDateText.Text = _updateInfo.FormattedDate;
        ReleaseNotesText.Text = string.IsNullOrWhiteSpace(_updateInfo.ReleaseNotes) 
            ? "No release notes available." 
            : _updateInfo.ReleaseNotes;

        // Add prerelease indicator if needed
        if (_updateInfo.IsPrerelease)
        {
            NewVersionText.Text += " (Pre-release)";
        }
    }

    private async void InstallUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isInstalling) return;

        _isInstalling = true;
        ShouldInstall = true;

        // Show progress UI
        ActionButtonsPanel.Visibility = Visibility.Collapsed;
        ProgressCard.Visibility = Visibility.Visible;

        // Create progress reporter
        var progress = new Progress<DownloadProgress>(report =>
        {
            Dispatcher.Invoke(() =>
            {
                ProgressStatusText.Text = report.Message;
                ProgressBar.Value = report.PercentageComplete;
            });
        });

        try
        {
            var success = await _updateService.DownloadAndInstallUpdateAsync(_updateInfo, progress);
            
            if (success)
            {
                // The application should be closing/restarting at this point
                Dispatcher.Invoke(() =>
                {
                    ProgressStatusText.Text = "Restarting Modrix...";
                    ProgressBar.Value = 100;
                });

                // Give a moment for the UI to update, then close
                await Task.Delay(1000);
                Application.Current.Shutdown();
            }
            else
            {
                // Show error and restore UI
                Dispatcher.Invoke(() =>
                {
                    var errorDialog = new Wpf.Ui.Controls.MessageBox
                    {
                        Title = "Update Failed",
                        Content = "The update could not be installed. Please try downloading manually from GitHub.",
                        PrimaryButtonText = "OK",
                        Owner = this
                    };
                    errorDialog.ShowDialogAsync();

                    RestoreUI();
                });
            }
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                var errorDialog = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "Update Error",
                    Content = $"An error occurred while updating: {ex.Message}",
                    PrimaryButtonText = "OK",
                    Owner = this
                };
                errorDialog.ShowDialogAsync();

                RestoreUI();
            });
        }
    }

    private void RestoreUI()
    {
        _isInstalling = false;
        ProgressCard.Visibility = Visibility.Collapsed;
        ActionButtonsPanel.Visibility = Visibility.Visible;
        ProgressBar.Value = 0;
        ProgressStatusText.Text = "Preparing...";
    }

    private void RemindLaterButton_Click(object sender, RoutedEventArgs e)
    {
        ShouldInstall = false;
        ShouldSkipVersion = false;
        Close();
    }

    private void SkipVersionButton_Click(object sender, RoutedEventArgs e)
    {
        ShouldInstall = false;
        ShouldSkipVersion = true;
        Close();
    }
}