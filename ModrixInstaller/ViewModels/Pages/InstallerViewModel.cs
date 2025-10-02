using ModrixInstaller.Models;
using ModrixInstaller.Services;
using ModrixInstaller.ViewModels.Pages;
using ModrixInstaller.Views.Dialogs;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Wpf.Ui;
using Wpf.Ui.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ModrixInstaller.ViewModels.Pages;

public partial class InstallerViewModel : ObservableObject
{
    private readonly IGitHubService _gitHubService;
    private readonly IInstallationService _installationService;
    private ShortcutsViewModel? _shortcutsSettings;

    [ObservableProperty]
    private ObservableCollection<GitHubRelease> _releases = new();

    [ObservableProperty]
    private GitHubRelease? _selectedRelease;

    [ObservableProperty]
    private string _installationPath = "";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private string _installationStatus = "";

    [ObservableProperty]
    private int _installationProgress;

    [ObservableProperty]
    private bool _installationCompleted;

    [ObservableProperty]
    private bool _isAdministrator;

    public InstallerViewModel(
        IGitHubService gitHubService,
        IInstallationService installationService)
    {
        _gitHubService = gitHubService;
        _installationService = installationService;

        InstallationPath = _installationService.GetDefaultInstallationPath();
        IsAdministrator = _installationService.IsRunningAsAdministrator();
    }

    public void SetShortcutsSettings(ShortcutsViewModel shortcutsSettings)
    {
        _shortcutsSettings = shortcutsSettings;
    }

    public void InitializeIfNeeded()
    {
        if (Releases.Count == 0 && !IsLoading)
        {
            LoadReleasesCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task LoadReleases()
    {
        IsLoading = true;
        InstallationStatus = "Loading releases...";

        try
        {
            var releases = await _gitHubService.GetReleasesAsync();
            Releases.Clear();
            foreach (var release in releases) Releases.Add(release);

            if (Releases.Count > 0)
            {
                SelectedRelease = Releases.First();
                InstallationStatus = $"Found {Releases.Count} release(s). Select a version to install.";
            }
            else InstallationStatus = "No releases found with Modrix.exe files.";
        }
        catch (Exception ex)
        {
            InstallationStatus = $"Failed to load releases: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void BrowseInstallationPath()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Select Installation Directory",
            CheckFileExists = false,
            CheckPathExists = false,
            FileName = "Select Folder",
            Filter = "Folder Selection|*.folder"
        };
        if (dialog.ShowDialog() == true)
        {
            var selectedPath = Path.GetDirectoryName(dialog.FileName);
            if (!string.IsNullOrEmpty(selectedPath)) InstallationPath = selectedPath;
        }
    }

    [RelayCommand]
    private async Task RequestAdministratorAsync()
    {
        InstallationStatus = "Requesting administrator privileges...";
        var success = await _installationService.RequestAdministratorPrivilegesAsync();
        if (success)
        {
            InstallationStatus = "Restarting with administrator privileges...";
            Application.Current.Shutdown();
        }
        else
        {
            InstallationStatus = "Failed to obtain administrator privileges.";
        }
    }

    [RelayCommand]
    private async Task InstallModrixAsync()
    {
        if (SelectedRelease == null)
        {
            InstallationStatus = "Please select a release to install.";
            return;
        }
        if (!_installationService.IsValidInstallationPath(InstallationPath))
        {
            InstallationStatus = "Please select a valid installation path.";
            return;
        }

        IsInstalling = true;
        InstallationCompleted = false;
        InstallationProgress = 0;

        var progress = new Progress<string>(status =>
        {
            InstallationStatus = status;
            if (status.Contains("Preparing")) InstallationProgress = 10;
            else if (status.Contains("Downloading"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(status, @"(\d+)%");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var percentage))
                    InstallationProgress = 20 + (percentage * 40 / 100);
                else InstallationProgress = 40;
            }
            else if (status.Contains("Creating")) InstallationProgress = 65;
            else if (status.Contains("Adding")) InstallationProgress = 75;
            else if (status.Contains("Registering")) InstallationProgress = 85;
            else if (status.Contains("Attempting")) InstallationProgress = 95;
            else if (status.Contains("completed")) InstallationProgress = 100;
        });

        try
        {
            // Use shortcut settings, or create default if none provided
            var shortcuts = _shortcutsSettings ?? new ShortcutsViewModel();
            
            await _installationService.InstallModrixAsync(SelectedRelease, InstallationPath, shortcuts, progress);
            InstallationCompleted = true;
            
            // Show success dialog
            var modrixPath = Path.Combine(InstallationPath, "Modrix.exe");
            var successDialog = new SuccessDialog(modrixPath)
            {
                Owner = Application.Current.MainWindow
            };
            successDialog.ShowDialog();
        }
        catch (Exception ex)
        {
            InstallationStatus = $"Installation failed: {ex.Message}";
            
            // Show failed dialog
            var failedDialog = new FailedDialog(ex.Message)
            {
                Owner = Application.Current.MainWindow
            };
            failedDialog.ShowDialog();
            
            // If user wants to retry
            if (failedDialog.ShouldRetry)
            {
                // Reset state and try again
                await Task.Delay(1000); // Brief delay
                await InstallModrixAsync();
                return;
            }
        }
        finally { IsInstalling = false; }
    }

    [RelayCommand]
    private void LaunchModrix()
    {
        try
        {
            var modrixPath = Path.Combine(InstallationPath, "Modrix.exe");
            if (File.Exists(modrixPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = modrixPath, UseShellExecute = true });
                InstallationStatus = "Modrix launched successfully!";
            }
            else InstallationStatus = "Modrix.exe not found in installation directory.";
        }
        catch (Exception ex)
        {
            InstallationStatus = $"Failed to launch Modrix: {ex.Message}";
        }
    }
}