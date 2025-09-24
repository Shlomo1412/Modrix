using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ModrixInstaller.Services;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ModrixInstaller.ViewModels.Pages
{
    public partial class CompletePageViewModel : ObservableObject
    {
        private readonly ConfigurationService _configurationService;

        [ObservableProperty]
        private string _completionTitle = "Installation Completed Successfully!";

        [ObservableProperty]
        private string _completionMessage = 
            "Modrix has been successfully installed on your computer.\n\n" +
            "You can now start creating amazing Minecraft mods with the powerful tools and features that Modrix provides.";

        [ObservableProperty]
        private bool _runAfterInstall;

        [ObservableProperty]
        private string _installationPath = string.Empty;

        [ObservableProperty]
        private string _installationSummary = string.Empty;

        [ObservableProperty]
        private bool _showReleaseNotes;

        [ObservableProperty]
        private string _releaseNotesUrl = "https://github.com/Shlomo1412/Modrix/releases";

        public CompletePageViewModel(ConfigurationService configurationService)
        {
            _configurationService = configurationService;
            
            var config = _configurationService.Configuration;
            _runAfterInstall = config.RunAfterInstall;
            _installationPath = config.InstallPath;

            GenerateInstallationSummary();
        }

        [RelayCommand]
        private void LaunchModrix()
        {
            try
            {
                var exePath = Path.Combine(InstallationPath, "Modrix.exe");
                if (File.Exists(exePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                // Log error or show message
            }
        }

        [RelayCommand]
        private void OpenInstallationFolder()
        {
            try
            {
                if (Directory.Exists(InstallationPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = InstallationPath,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                // Log error or show message
            }
        }

        [RelayCommand]
        private void OpenWebsite()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/Shlomo1412/Modrix",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                // Log error or show message
            }
        }

        [RelayCommand]
        private void ViewReleaseNotes()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = ReleaseNotesUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                // Log error or show message
            }
        }

        [RelayCommand]
        private void Finish()
        {
            if (RunAfterInstall)
            {
                LaunchModrix();
            }
            
            Application.Current.Shutdown();
        }

        private void GenerateInstallationSummary()
        {
            var config = _configurationService.Configuration;
            var summary = new List<string>();

            summary.Add($"Installation Location: {config.InstallPath}");
            
            if (config.CreateDesktopShortcut)
                summary.Add("? Desktop shortcut created");
            
            if (config.CreateStartMenuShortcut)
                summary.Add("? Start menu shortcut created");
            
            if (config.CheckForUpdates)
                summary.Add("? Automatic updates enabled");

            summary.Add($"Language: {config.SelectedLanguage}");

            InstallationSummary = string.Join("\n", summary);
        }

        partial void OnRunAfterInstallChanged(bool value)
        {
            _configurationService.UpdateConfiguration(config =>
            {
                config.RunAfterInstall = value;
            });
        }
    }
}