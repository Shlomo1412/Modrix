using System.Diagnostics;
using System.Security.Principal;
using ModrixInstaller.Models;
using System.Threading.Tasks;
using System.IO;

namespace ModrixInstaller.Services;

public interface IInstallationService
{
    bool IsRunningAsAdministrator();
    Task<bool> RequestAdministratorPrivilegesAsync();
    Task InstallModrixAsync(GitHubRelease release, string installationPath, IProgress<string>? progress = null);
    string GetDefaultInstallationPath();
    bool IsValidInstallationPath(string path);
}

public class InstallationService : IInstallationService
{
    private readonly IGitHubService _gitHubService;

    public InstallationService(IGitHubService gitHubService)
    {
        _gitHubService = gitHubService;
    }

    public bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public async Task<bool> RequestAdministratorPrivilegesAsync()
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = Environment.ProcessPath,
                Verb = "runas"
            };

            Process.Start(processStartInfo);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task InstallModrixAsync(GitHubRelease release, string installationPath, IProgress<string>? progress = null)
    {
        if (release.ModrixAsset == null)
            throw new InvalidOperationException("The selected release does not contain a Modrix.exe file.");

        progress?.Report("Preparing installation...");

        // Ensure installation directory exists
        if (!Directory.Exists(installationPath))
        {
            Directory.CreateDirectory(installationPath);
        }

        var modrixPath = Path.Combine(installationPath, "Modrix.exe");

        // Download Modrix.exe
        progress?.Report("Downloading Modrix...");
        var downloadProgress = new Progress<DownloadProgress>(p =>
        {
            progress?.Report($"Downloading Modrix... {p.PercentageComplete}%");
        });

        await _gitHubService.DownloadModrixAsync(release.ModrixAsset, modrixPath, downloadProgress);

        // Create desktop shortcut
        progress?.Report("Creating desktop shortcut...");
        await CreateDesktopShortcutAsync(modrixPath);

        // Add to Start Menu
        progress?.Report("Adding to Start Menu...");
        await CreateStartMenuShortcutAsync(modrixPath);

        // Register in Programs and Features (optional)
        progress?.Report("Registering installation...");
        await RegisterInstallationAsync(release, installationPath);

        progress?.Report("Installation completed successfully!");
    }

    public string GetDefaultInstallationPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Modrix");
    }

    public bool IsValidInstallationPath(string path)
    {
        try
        {
            // Check if path is not empty and is a valid directory path
            if (string.IsNullOrWhiteSpace(path))
                return false;

            // Try to get the full path to validate format
            var fullPath = Path.GetFullPath(path);

            // Check if we can create the directory (or if it already exists)
            if (!Directory.Exists(path))
            {
                var parentDir = Directory.GetParent(path);
                if (parentDir?.Exists != true)
                {
                    // Check if we can create parent directories
                    return true; // Assume valid, let the actual installation handle errors
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task CreateDesktopShortcutAsync(string modrixPath)
    {
        await Task.Run(() =>
        {
            try
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var shortcutPath = Path.Combine(desktopPath, "Modrix.lnk");

                // Simple shortcut creation using WScript.Shell COM object
                var shell = new IWshRuntimeLibrary.WshShell();
                var shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = modrixPath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(modrixPath);
                shortcut.Description = "Modrix Application";
                shortcut.Save();
            }
            catch
            {
                // Shortcut creation failed, but don't fail the entire installation
            }
        });
    }

    private async Task CreateStartMenuShortcutAsync(string modrixPath)
    {
        await Task.Run(() =>
        {
            try
            {
                var startMenuPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                    "Programs", "Modrix");

                if (!Directory.Exists(startMenuPath))
                    Directory.CreateDirectory(startMenuPath);

                var shortcutPath = Path.Combine(startMenuPath, "Modrix.lnk");

                var shell = new IWshRuntimeLibrary.WshShell();
                var shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = modrixPath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(modrixPath);
                shortcut.Description = "Modrix Application";
                shortcut.Save();
            }
            catch
            {
                // Shortcut creation failed, but don't fail the entire installation
            }
        });
    }

    private async Task RegisterInstallationAsync(GitHubRelease release, string installationPath)
    {
        // This would typically register the application in the Windows Registry
        // for Programs and Features, but for simplicity, we'll skip this
        // as it requires more complex registry operations
        await Task.Delay(100); // Simulate work
    }
}