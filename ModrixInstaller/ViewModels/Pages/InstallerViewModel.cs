using ModrixInstaller.Models;
using ModrixInstaller.Services;
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
    private readonly ISnackbarService _snackbarService;

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
        IInstallationService installationService,
        ISnackbarService snackbarService)
    {
        _gitHubService = gitHubService;
        _installationService = installationService;
        _snackbarService = snackbarService;

        InstallationPath = _installationService.GetDefaultInstallationPath();
        IsAdministrator = _installationService.IsRunningAsAdministrator();
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
            _snackbarService.Show("Error", "Failed to load releases from GitHub.", ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
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
            _snackbarService.Show("Error", "Administrator privileges are required for installation.", ControlAppearance.Caution, null, TimeSpan.FromSeconds(5));
        }
    }

    [RelayCommand]
    private async Task InstallModrixAsync()
    {
        if (SelectedRelease == null)
        {
            _snackbarService.Show("Error", "Please select a release to install.", ControlAppearance.Caution, null, TimeSpan.FromSeconds(3));
            return;
        }
        if (!_installationService.IsValidInstallationPath(InstallationPath))
        {
            _snackbarService.Show("Error", "Please select a valid installation path.", ControlAppearance.Caution, null, TimeSpan.FromSeconds(3));
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
                    InstallationProgress = 20 + (percentage * 60 / 100);
                else InstallationProgress = 50;
            }
            else if (status.Contains("Creating")) InstallationProgress = 85;
            else if (status.Contains("Adding")) InstallationProgress = 90;
            else if (status.Contains("Registering")) InstallationProgress = 95;
            else if (status.Contains("completed")) InstallationProgress = 100;
        });

        try
        {
            await _installationService.InstallModrixAsync(SelectedRelease, InstallationPath, progress);
            InstallationCompleted = true;
            _snackbarService.Show("Success", "Modrix has been installed successfully!", ControlAppearance.Success, null, TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            InstallationStatus = $"Installation failed: {ex.Message}";
            _snackbarService.Show("Error", $"Installation failed: {ex.Message}", ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
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
                _snackbarService.Show("Success", "Modrix has been launched!", ControlAppearance.Success, null, TimeSpan.FromSeconds(3));
            }
            else _snackbarService.Show("Error", "Modrix.exe not found in installation directory.", ControlAppearance.Danger, null, TimeSpan.FromSeconds(3));
        }
        catch (Exception ex)
        {
            _snackbarService.Show("Error", $"Failed to launch Modrix: {ex.Message}", ControlAppearance.Danger, null, TimeSpan.FromSeconds(3));
        }
    }
}