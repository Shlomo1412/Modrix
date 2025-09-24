using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ModrixInstaller.Services;
using Microsoft.Win32;
using System.IO;

namespace ModrixInstaller.ViewModels.Pages
{
    public partial class InstallationOptionsViewModel : ObservableObject
    {
        private readonly ConfigurationService _configurationService;

        [ObservableProperty]
        private string _installPath;

        [ObservableProperty]
        private bool _createDesktopShortcut;

        [ObservableProperty]
        private bool _createStartMenuShortcut;

        [ObservableProperty]
        private bool _runAfterInstall;

        [ObservableProperty]
        private bool _checkForUpdates;

        [ObservableProperty]
        private bool _sendUsageStatistics;

        [ObservableProperty]
        private string _selectedLanguage = "English";

        [ObservableProperty]
        private string _requiredSpace = "150 MB";

        [ObservableProperty]
        private string _availableSpace = "Calculating...";

        [ObservableProperty]
        private bool _hasEnoughSpace = true;

        [ObservableProperty]
        private string _installPathValidationMessage = string.Empty;

        [ObservableProperty]
        private bool _isInstallPathValid = true;

        public InstallationOptionsViewModel(ConfigurationService configurationService)
        {
            _configurationService = configurationService;
            
            var config = _configurationService.Configuration;
            _installPath = string.IsNullOrEmpty(config.InstallPath) ? _configurationService.GetDefaultInstallPath() : config.InstallPath;
            _createDesktopShortcut = config.CreateDesktopShortcut;
            _createStartMenuShortcut = config.CreateStartMenuShortcut;
            _runAfterInstall = config.RunAfterInstall;
            _checkForUpdates = config.CheckForUpdates;
            _sendUsageStatistics = config.SendUsageStatistics;
            _selectedLanguage = config.SelectedLanguage;

            UpdateDiskSpaceInfo();
            ValidateInstallPath();
        }

        [RelayCommand]
        private void BrowseInstallPath()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Installation Folder",
                InitialDirectory = InstallPath
            };

            if (dialog.ShowDialog() == true)
            {
                InstallPath = dialog.FolderName;
            }
        }

        [RelayCommand]
        private void ResetToDefault()
        {
            InstallPath = _configurationService.GetDefaultInstallPath();
        }

        partial void OnInstallPathChanged(string value)
        {
            UpdateConfiguration();
            ValidateInstallPath();
            UpdateDiskSpaceInfo();
        }

        partial void OnCreateDesktopShortcutChanged(bool value)
        {
            UpdateConfiguration();
        }

        partial void OnCreateStartMenuShortcutChanged(bool value)
        {
            UpdateConfiguration();
        }

        partial void OnRunAfterInstallChanged(bool value)
        {
            UpdateConfiguration();
        }

        partial void OnCheckForUpdatesChanged(bool value)
        {
            UpdateConfiguration();
        }

        partial void OnSendUsageStatisticsChanged(bool value)
        {
            UpdateConfiguration();
        }

        partial void OnSelectedLanguageChanged(string value)
        {
            UpdateConfiguration();
        }

        private void UpdateConfiguration()
        {
            _configurationService.UpdateConfiguration(config =>
            {
                config.InstallPath = InstallPath;
                config.CreateDesktopShortcut = CreateDesktopShortcut;
                config.CreateStartMenuShortcut = CreateStartMenuShortcut;
                config.RunAfterInstall = RunAfterInstall;
                config.CheckForUpdates = CheckForUpdates;
                config.SendUsageStatistics = SendUsageStatistics;
                config.SelectedLanguage = SelectedLanguage;
            });
        }

        private void ValidateInstallPath()
        {
            if (string.IsNullOrWhiteSpace(InstallPath))
            {
                IsInstallPathValid = false;
                InstallPathValidationMessage = "Installation path cannot be empty.";
                return;
            }

            try
            {
                // Check if path is valid
                Path.GetFullPath(InstallPath);

                // Check if it's a system directory
                var systemDirs = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                };

                if (systemDirs.Any(dir => InstallPath.StartsWith(dir, StringComparison.OrdinalIgnoreCase)) && 
                    !InstallPath.Contains("\\Modrix", StringComparison.OrdinalIgnoreCase))
                {
                    IsInstallPathValid = false;
                    InstallPathValidationMessage = "Cannot install directly to system directories. Please choose a subfolder.";
                    return;
                }

                if (_configurationService.IsValidInstallPath(InstallPath))
                {
                    IsInstallPathValid = true;
                    InstallPathValidationMessage = string.Empty;
                }
                else
                {
                    IsInstallPathValid = false;
                    InstallPathValidationMessage = "Cannot write to the selected location. Please choose a different folder.";
                }
            }
            catch
            {
                IsInstallPathValid = false;
                InstallPathValidationMessage = "Invalid path format.";
            }
        }

        private void UpdateDiskSpaceInfo()
        {
            try
            {
                var required = _configurationService.GetRequiredDiskSpace();
                var available = _configurationService.GetAvailableDiskSpace(InstallPath);

                RequiredSpace = FormatBytes(required);
                AvailableSpace = FormatBytes(available);
                HasEnoughSpace = available >= required;
            }
            catch
            {
                AvailableSpace = "Unknown";
                HasEnoughSpace = true; // Assume it's OK if we can't determine
            }
        }

        private static string FormatBytes(long bytes)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            return bytes switch
            {
                >= GB => $"{bytes / (double)GB:F1} GB",
                >= MB => $"{bytes / (double)MB:F1} MB",
                >= KB => $"{bytes / (double)KB:F1} KB",
                _ => $"{bytes} bytes"
            };
        }

        public bool CanProceed => IsInstallPathValid && HasEnoughSpace;
    }
}