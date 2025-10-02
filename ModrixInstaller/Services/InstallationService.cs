using System.Diagnostics;
using System.Security.Principal;
using ModrixInstaller.Models;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using ModrixInstaller.ViewModels.Pages;

namespace ModrixInstaller.Services;

public interface IInstallationService
{
    bool IsRunningAsAdministrator();
    Task<bool> RequestAdministratorPrivilegesAsync();
    Task InstallModrixAsync(GitHubRelease release, string installationPath, ShortcutsViewModel shortcutsSettings, IProgress<string>? progress = null);
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

    public async Task InstallModrixAsync(GitHubRelease release, string installationPath, ShortcutsViewModel shortcutsSettings, IProgress<string>? progress = null)
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

        // Create shortcuts based on user preferences
        if (shortcutsSettings.CreateDesktopShortcut)
        {
            progress?.Report("Creating desktop shortcut...");
            await CreateDesktopShortcutAsync(modrixPath);
        }

        if (shortcutsSettings.CreateStartMenuShortcut)
        {
            progress?.Report("Adding to Start Menu...");
            await CreateStartMenuShortcutAsync(modrixPath);
        }

        if (shortcutsSettings.CreateQuickLaunchShortcut)
        {
            progress?.Report("Adding to Quick Launch...");
            await CreateQuickLaunchShortcutAsync(modrixPath);
        }

        // Register file associations
        if (shortcutsSettings.AssociateProjectFiles)
        {
            progress?.Report("Registering file associations...");
            await RegisterFileAssociationsAsync(modrixPath);
        }

        // Add to system PATH
        if (shortcutsSettings.AddToSystemPath)
        {
            progress?.Report("Adding to system PATH...");
            await AddToSystemPathAsync(installationPath);
        }

        // Register in Programs and Features (Windows Add/Remove Programs)
        progress?.Report("Registering installation...");
        await RegisterInstallationAsync(release, installationPath, modrixPath);

        // Pin to taskbar (note: this is limited by Windows security policies)
        if (shortcutsSettings.PinToTaskbar)
        {
            progress?.Report("Attempting to pin to taskbar...");
            await AttemptTaskbarPinAsync(modrixPath);
        }

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

                CreateShortcut(shortcutPath, modrixPath, Path.GetDirectoryName(modrixPath) ?? "", "Modrix - Minecraft Mod Development IDE");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create desktop shortcut: {ex.Message}");
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

                CreateShortcut(shortcutPath, modrixPath, Path.GetDirectoryName(modrixPath) ?? "", "Modrix - Minecraft Mod Development IDE");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create start menu shortcut: {ex.Message}");
                // Shortcut creation failed, but don't fail the entire installation
            }
        });
    }

    private async Task CreateQuickLaunchShortcutAsync(string modrixPath)
    {
        await Task.Run(() =>
        {
            try
            {
                var quickLaunchPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Microsoft", "Internet Explorer", "Quick Launch");

                if (!Directory.Exists(quickLaunchPath))
                    return; // Quick Launch not available

                var shortcutPath = Path.Combine(quickLaunchPath, "Modrix.lnk");

                CreateShortcut(shortcutPath, modrixPath, Path.GetDirectoryName(modrixPath) ?? "", "Modrix - Minecraft Mod Development IDE");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create quick launch shortcut: {ex.Message}");
                // Shortcut creation failed, but don't fail the entire installation
            }
        });
    }

    private async Task RegisterFileAssociationsAsync(string modrixPath)
    {
        await Task.Run(() =>
        {
            try
            {
                using var key = Registry.ClassesRoot.CreateSubKey(".modrix");
                key?.SetValue("", "ModrixProject");

                using var projectKey = Registry.ClassesRoot.CreateSubKey("ModrixProject");
                projectKey?.SetValue("", "Modrix Project File");

                using var iconKey = projectKey?.CreateSubKey("DefaultIcon");
                iconKey?.SetValue("", modrixPath + ",0");

                using var commandKey = projectKey?.CreateSubKey(@"shell\open\command");
                commandKey?.SetValue("", $"\"{modrixPath}\" \"%1\"");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to register file associations: {ex.Message}");
            }
        });
    }

    private async Task AddToSystemPathAsync(string installationPath)
    {
        await Task.Run(() =>
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", true);
                if (key != null)
                {
                    var currentPath = key.GetValue("Path")?.ToString() ?? "";
                    if (!currentPath.Contains(installationPath))
                    {
                        var newPath = string.IsNullOrEmpty(currentPath) 
                            ? installationPath 
                            : $"{currentPath};{installationPath}";
                        key.SetValue("Path", newPath, RegistryValueKind.ExpandString);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to add to system PATH: {ex.Message}");
            }
        });
    }

    private async Task AttemptTaskbarPinAsync(string modrixPath)
    {
        await Task.Run(() =>
        {
            try
            {
                // Note: Modern Windows restricts programmatic taskbar pinning
                // This creates a script that the user can run to pin manually
                var scriptPath = Path.Combine(Path.GetTempPath(), "ModrixTaskbarPin.ps1");
                var scriptContent = $@"
# PowerShell script to pin Modrix to taskbar
$shell = New-Object -ComObject Shell.Application
$folder = $shell.Namespace('{Path.GetDirectoryName(modrixPath)}')
$item = $folder.ParseName('Modrix.exe')
$item.InvokeVerb('taskbarpin')
";
                File.WriteAllText(scriptPath, scriptContent);
                
                // Try to execute (may fail due to execution policy)
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-File \"{scriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var process = Process.Start(processStartInfo);
                process?.WaitForExit(5000);

                // Clean up
                try { File.Delete(scriptPath); } catch { }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to pin to taskbar: {ex.Message}");
            }
        });
    }

    private async Task RegisterInstallationAsync(GitHubRelease release, string installationPath, string modrixPath)
    {
        await Task.Run(() =>
        {
            try
            {
                var uninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Modrix";
                
                using var key = Registry.LocalMachine.CreateSubKey(uninstallKey);
                if (key != null)
                {
                    key.SetValue("DisplayName", "Modrix");
                    key.SetValue("DisplayVersion", release.TagName ?? "Unknown");
                    key.SetValue("Publisher", "Modrix Team");
                    key.SetValue("InstallLocation", installationPath);
                    key.SetValue("DisplayIcon", modrixPath);
                    key.SetValue("UninstallString", $"\"{modrixPath}\" --uninstall");
                    key.SetValue("QuietUninstallString", $"\"{modrixPath}\" --uninstall --quiet");
                    key.SetValue("ModifyPath", $"\"{modrixPath}\" --modify");
                    key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                    key.SetValue("NoModify", 0, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 0, RegistryValueKind.DWord);
                    key.SetValue("SystemComponent", 0, RegistryValueKind.DWord);
                    
                    // Calculate estimated size (in KB)
                    if (File.Exists(modrixPath))
                    {
                        var fileSize = new FileInfo(modrixPath).Length;
                        var sizeKB = (int)(fileSize / 1024);
                        key.SetValue("EstimatedSize", sizeKB, RegistryValueKind.DWord);
                    }

                    // URL Info
                    key.SetValue("URLInfoAbout", "https://github.com/Shlomo1412/Modrix");
                    key.SetValue("URLUpdateInfo", "https://github.com/Shlomo1412/Modrix/releases");
                    key.SetValue("HelpLink", "https://github.com/Shlomo1412/Modrix/issues");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to register installation: {ex.Message}");
                // Registration failed, but don't fail the entire installation
            }
        });
    }

    // .NET-compatible shortcut creation using Shell32
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, 
        ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory, string description)
    {
        try
        {
            // Use PowerShell to create the shortcut as a fallback method
            var powershellScript = $@"
$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut('{shortcutPath}')
$Shortcut.TargetPath = '{targetPath}'
$Shortcut.WorkingDirectory = '{workingDirectory}'
$Shortcut.Description = '{description}'
$Shortcut.Save()
";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{powershellScript}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            process.WaitForExit(5000); // Wait max 5 seconds

            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                System.Diagnostics.Debug.WriteLine($"PowerShell shortcut creation failed: {error}");
                
                // Fallback: create a simple batch file that launches the target
                CreateBatchShortcut(shortcutPath.Replace(".lnk", ".bat"), targetPath, workingDirectory);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Shortcut creation failed: {ex.Message}");
            
            // Final fallback: create a simple batch file
            try
            {
                CreateBatchShortcut(shortcutPath.Replace(".lnk", ".bat"), targetPath, workingDirectory);
            }
            catch (Exception batchEx)
            {
                System.Diagnostics.Debug.WriteLine($"Batch shortcut creation also failed: {batchEx.Message}");
            }
        }
    }

    private static void CreateBatchShortcut(string batchPath, string targetPath, string workingDirectory)
    {
        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine($"cd /d \"{workingDirectory}\"");
        sb.AppendLine($"start \"\" \"{targetPath}\"");
        
        File.WriteAllText(batchPath, sb.ToString());
    }
}