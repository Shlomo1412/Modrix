using ModrixInstaller.Models;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Microsoft.Win32;
using System.Reflection;

namespace ModrixInstaller.Services
{
    public class InstallationService
    {
        public event EventHandler<InstallationProgressEventArgs>? ProgressChanged;
        public event EventHandler<string>? StatusChanged;

        public async Task<bool> InstallAsync(InstallationConfiguration config)
        {
            try
            {
                OnStatusChanged("Starting installation...");
                OnProgressChanged(0, "Initializing...");

                // Step 1: Create installation directory
                OnStatusChanged("Creating installation directory...");
                await CreateInstallationDirectory(config.InstallPath);
                OnProgressChanged(10, "Installation directory created");

                // Step 2: Extract files
                OnStatusChanged("Extracting application files...");
                await ExtractApplicationFiles(config.InstallPath);
                OnProgressChanged(40, "Application files extracted");

                // Step 3: Create shortcuts
                if (config.CreateDesktopShortcut)
                {
                    OnStatusChanged("Creating desktop shortcut...");
                    await CreateDesktopShortcut(config.InstallPath);
                    OnProgressChanged(60, "Desktop shortcut created");
                }

                if (config.CreateStartMenuShortcut)
                {
                    OnStatusChanged("Creating start menu shortcut...");
                    await CreateStartMenuShortcut(config.InstallPath);
                    OnProgressChanged(70, "Start menu shortcut created");
                }

                // Step 4: Register application
                OnStatusChanged("Registering application...");
                await RegisterApplication(config);
                OnProgressChanged(85, "Application registered");

                // Step 5: Create uninstaller
                OnStatusChanged("Creating uninstaller...");
                await CreateUninstaller(config.InstallPath);
                OnProgressChanged(95, "Uninstaller created");

                OnStatusChanged("Installation completed successfully!");
                OnProgressChanged(100, "Installation complete");

                return true;
            }
            catch (Exception ex)
            {
                OnStatusChanged($"Installation failed: {ex.Message}");
                return false;
            }
        }

        private async Task CreateInstallationDirectory(string installPath)
        {
            await Task.Run(() =>
            {
                if (Directory.Exists(installPath))
                {
                    Directory.Delete(installPath, true);
                }
                Directory.CreateDirectory(installPath);
            });
        }

        private async Task ExtractApplicationFiles(string installPath)
        {
            await Task.Run(() =>
            {
                // Get the embedded Modrix.exe from resources
                var executingAssembly = Assembly.GetExecutingAssembly();
                var resourceName = "ModrixInstaller.exe.Modrix.exe";

                // Check if embedded resource exists
                using var stream = executingAssembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    var targetPath = Path.Combine(installPath, "Modrix.exe");
                    using var fileStream = File.Create(targetPath);
                    stream.CopyTo(fileStream);
                }
                else
                {
                    // Fallback: copy from exe folder if it exists
                    var exeFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exe");
                    if (Directory.Exists(exeFolderPath))
                    {
                        CopyDirectory(exeFolderPath, installPath);
                    }
                    else
                    {
                        throw new FileNotFoundException("Modrix.exe not found in installer resources or exe folder");
                    }
                }

                // Copy other necessary files (if any)
                var currentDir = AppDomain.CurrentDomain.BaseDirectory;
                var iconSource = Path.Combine(currentDir, "Assets", "ModrixIcon.ico");
                if (File.Exists(iconSource))
                {
                    File.Copy(iconSource, Path.Combine(installPath, "ModrixIcon.ico"), true);
                }
            });
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var dest = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, dest, true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dest = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, dest);
            }
        }

        private async Task CreateDesktopShortcut(string installPath)
        {
            await Task.Run(() =>
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var shortcutPath = Path.Combine(desktopPath, "Modrix.lnk");
                var targetPath = Path.Combine(installPath, "Modrix.exe");
                
                CreateShortcut(shortcutPath, targetPath, "Modrix - Minecraft Mod Development IDE");
            });
        }

        private async Task CreateStartMenuShortcut(string installPath)
        {
            await Task.Run(() =>
            {
                var startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
                var modrixFolder = Path.Combine(startMenuPath, "Programs", "Modrix");
                Directory.CreateDirectory(modrixFolder);
                
                var shortcutPath = Path.Combine(modrixFolder, "Modrix.lnk");
                var targetPath = Path.Combine(installPath, "Modrix.exe");
                
                CreateShortcut(shortcutPath, targetPath, "Modrix - Minecraft Mod Development IDE");
            });
        }

        private void CreateShortcut(string shortcutPath, string targetPath, string description)
        {
            var shell = new IWshRuntimeLibrary.WshShell();
            var shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.Description = description;
            shortcut.Save();
        }

        private async Task RegisterApplication(InstallationConfiguration config)
        {
            await Task.Run(() =>
            {
                try
                {
                    using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Modrix");
                    key.SetValue("DisplayName", "Modrix");
                    key.SetValue("DisplayVersion", "1.0.0");
                    key.SetValue("Publisher", "Modrix Development Team");
                    key.SetValue("InstallLocation", config.InstallPath);
                    key.SetValue("UninstallString", Path.Combine(config.InstallPath, "Uninstall.exe"));
                    key.SetValue("DisplayIcon", Path.Combine(config.InstallPath, "Modrix.exe"));
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
                catch (UnauthorizedAccessException)
                {
                    // Fallback to current user registry if no admin privileges
                    using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Modrix");
                    key.SetValue("DisplayName", "Modrix");
                    key.SetValue("DisplayVersion", "1.0.0");
                    key.SetValue("Publisher", "Modrix Development Team");
                    key.SetValue("InstallLocation", config.InstallPath);
                    key.SetValue("UninstallString", Path.Combine(config.InstallPath, "Uninstall.exe"));
                    key.SetValue("DisplayIcon", Path.Combine(config.InstallPath, "Modrix.exe"));
                }
            });
        }

        private async Task CreateUninstaller(string installPath)
        {
            await Task.Run(() =>
            {
                // Copy this installer as uninstaller
                var currentExe = Assembly.GetExecutingAssembly().Location;
                var uninstallerPath = Path.Combine(installPath, "Uninstall.exe");
                File.Copy(currentExe, uninstallerPath, true);
            });
        }

        public async Task<bool> UninstallAsync(string installPath)
        {
            try
            {
                OnStatusChanged("Starting uninstallation...");
                OnProgressChanged(0, "Initializing...");

                // Remove shortcuts
                OnStatusChanged("Removing shortcuts...");
                await RemoveShortcuts();
                OnProgressChanged(30, "Shortcuts removed");

                // Unregister application
                OnStatusChanged("Unregistering application...");
                await UnregisterApplication();
                OnProgressChanged(60, "Application unregistered");

                // Remove installation directory
                OnStatusChanged("Removing application files...");
                await RemoveInstallationDirectory(installPath);
                OnProgressChanged(100, "Uninstallation complete");

                return true;
            }
            catch (Exception ex)
            {
                OnStatusChanged($"Uninstallation failed: {ex.Message}");
                return false;
            }
        }

        private async Task RemoveShortcuts()
        {
            await Task.Run(() =>
            {
                try
                {
                    // Remove desktop shortcut
                    var desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Modrix.lnk");
                    if (File.Exists(desktopShortcut))
                        File.Delete(desktopShortcut);

                    // Remove start menu shortcuts
                    var startMenuFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Modrix");
                    if (Directory.Exists(startMenuFolder))
                        Directory.Delete(startMenuFolder, true);
                }
                catch { } // Ignore errors when removing shortcuts
            });
        }

        private async Task UnregisterApplication()
        {
            await Task.Run(() =>
            {
                try
                {
                    Registry.LocalMachine.DeleteSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Modrix", false);
                }
                catch
                {
                    try
                    {
                        Registry.CurrentUser.DeleteSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Modrix", false);
                    }
                    catch { } // Ignore errors
                }
            });
        }

        private async Task RemoveInstallationDirectory(string installPath)
        {
            await Task.Run(() =>
            {
                if (Directory.Exists(installPath))
                {
                    Directory.Delete(installPath, true);
                }
            });
        }

        private void OnProgressChanged(int percentage, string message)
        {
            ProgressChanged?.Invoke(this, new InstallationProgressEventArgs(percentage, message));
        }

        private void OnStatusChanged(string status)
        {
            StatusChanged?.Invoke(this, status);
        }
    }

    public class InstallationProgressEventArgs : EventArgs
    {
        public int Percentage { get; }
        public string Message { get; }

        public InstallationProgressEventArgs(int percentage, string message)
        {
            Percentage = percentage;
            Message = message;
        }
    }
}