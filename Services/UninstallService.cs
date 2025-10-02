using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace Modrix.Services
{
    public interface IUninstallService
    {
        Task<bool> UninstallModrixAsync(IProgress<(string Status, int Percentage)>? progress = null, bool deleteProjects = false);
        bool IsRunningAsAdministrator();
        Task<bool> RequestAdministratorPrivilegesAsync();
    }

    public class UninstallService : IUninstallService
    {
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
                    Arguments = "--uninstall",
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

        public async Task<bool> UninstallModrixAsync(IProgress<(string Status, int Percentage)>? progress = null, bool deleteProjects = false)
        {
            try
            {
                progress?.Report(("Starting uninstallation...", 0));
                await Task.Delay(500); // Brief pause for UI feedback
                
                var installLocation = GetInstallLocation();
                if (string.IsNullOrEmpty(installLocation))
                {
                    installLocation = AppDomain.CurrentDomain.BaseDirectory;
                }

                progress?.Report(("Removing registry entries...", 10));
                await RemoveRegistryEntriesAsync();
                await Task.Delay(300);

                progress?.Report(("Removing file associations...", 25));
                await RemoveFileAssociationsAsync();
                await Task.Delay(300);

                progress?.Report(("Removing from system PATH...", 40));
                await RemoveFromSystemPathAsync(installLocation);
                await Task.Delay(300);

                progress?.Report(("Removing shortcuts...", 55));
                await RemoveShortcutsAsync();
                await Task.Delay(300);

                if (deleteProjects)
                {
                    progress?.Report(("Removing user projects and data...", 70));
                    await RemoveUserDataAsync();
                    await Task.Delay(500);
                }

                progress?.Report(("Removing application files...", 85));
                await RemoveApplicationFilesAsync(installLocation);
                await Task.Delay(300);

                progress?.Report(("Finalizing uninstallation...", 95));
                await Task.Delay(500);

                progress?.Report(("Uninstallation completed successfully!", 100));
                await Task.Delay(1000); // Allow user to see completion
                return true;
            }
            catch (Exception ex)
            {
                progress?.Report(($"Uninstallation failed: {ex.Message}", 0));
                return false;
            }
        }

        private string? GetInstallLocation()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Modrix");
                return key?.GetValue("InstallLocation")?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private async Task RemoveRegistryEntriesAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    // Remove uninstall entry
                    using var uninstallKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", true);
                    uninstallKey?.DeleteSubKeyTree("Modrix", false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to remove registry entries: {ex.Message}");
                }
            });
        }

        private async Task RemoveFileAssociationsAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    // Remove .modrix file association
                    using var classesRoot = Registry.ClassesRoot;
                    classesRoot.DeleteSubKeyTree(".modrix", false);
                    classesRoot.DeleteSubKeyTree("ModrixProject", false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to remove file associations: {ex.Message}");
                }
            });
        }

        private async Task RemoveFromSystemPathAsync(string installLocation)
        {
            await Task.Run(() =>
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", true);
                    if (key != null)
                    {
                        var currentPath = key.GetValue("Path")?.ToString() ?? "";
                        var paths = currentPath.Split(';').Where(p => !string.Equals(p.Trim(), installLocation.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)).ToArray();
                        var newPath = string.Join(";", paths);
                        key.SetValue("Path", newPath, RegistryValueKind.ExpandString);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to remove from system PATH: {ex.Message}");
                }
            });
        }

        private async Task RemoveShortcutsAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    // Remove desktop shortcut
                    var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    var desktopShortcut = Path.Combine(desktopPath, "Modrix.lnk");
                    if (File.Exists(desktopShortcut))
                        File.Delete(desktopShortcut);

                    // Remove Start Menu shortcuts
                    var startMenuPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                        "Programs", "Modrix");
                    if (Directory.Exists(startMenuPath))
                        Directory.Delete(startMenuPath, true);

                    // Remove Quick Launch shortcut
                    var quickLaunchPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Microsoft", "Internet Explorer", "Quick Launch", "Modrix.lnk");
                    if (File.Exists(quickLaunchPath))
                        File.Delete(quickLaunchPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to remove shortcuts: {ex.Message}");
                }
            });
        }

        private async Task RemoveUserDataAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    // Remove user projects and data from %LocalAppData%\Modrix\
                    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var modrixDataPath = Path.Combine(localAppData, "Modrix");
                    
                    if (Directory.Exists(modrixDataPath))
                    {
                        Directory.Delete(modrixDataPath, true);
                    }

                    // Also check for any Modrix-related folders in user profile
                    var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    
                    // Remove any "Modrix Projects" folder in Documents if it exists
                    var documentsModrixPath = Path.Combine(documentsPath, "Modrix Projects");
                    if (Directory.Exists(documentsModrixPath))
                    {
                        Directory.Delete(documentsModrixPath, true);
                    }

                    // Remove any Roaming AppData for Modrix (settings, etc.)
                    var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    var roamingModrixPath = Path.Combine(roamingAppData, "Modrix");
                    if (Directory.Exists(roamingModrixPath))
                    {
                        Directory.Delete(roamingModrixPath, true);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to remove user data: {ex.Message}");
                    // Don't fail the entire uninstall if user data removal fails
                }
            });
        }

        private async Task RemoveApplicationFilesAsync(string installLocation)
        {
            await Task.Run(() =>
            {
                try
                {
                    // Create a batch file to delete the application files after the process exits
                    var batchPath = Path.Combine(Path.GetTempPath(), "ModrixUninstall.bat");
                    var batchContent = $@"@echo off
timeout /t 3 /nobreak >nul
rd /s /q ""{installLocation}""
del ""{batchPath}""
";
                    File.WriteAllText(batchPath, batchContent);

                    // Start the batch file
                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = batchPath,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    Process.Start(processStartInfo);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to schedule file removal: {ex.Message}");
                }
            });
        }
    }
}