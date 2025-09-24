using ModrixInstaller.Models;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Microsoft.Win32;
using System.Reflection;
using System.Security.Principal;

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

                // Check for admin privileges if installing to Program Files
                if (RequiresAdminPrivileges(config.InstallPath))
                {
                    if (!IsRunAsAdmin())
                    {
                        OnStatusChanged("Requesting administrator privileges...");
                        return RestartAsAdmin();
                    }
                }

                // Step 1: Create installation directory
                OnStatusChanged("Creating installation directory...");
                await CreateInstallationDirectory(config.InstallPath);
                OnProgressChanged(15, "Installation directory created");

                // Step 2: Extract files
                OnStatusChanged("Extracting application files...");
                await ExtractApplicationFiles(config.InstallPath);
                OnProgressChanged(50, "Application files extracted");

                // Step 3: Create shortcuts
                if (config.CreateDesktopShortcut)
                {
                    OnStatusChanged("Creating desktop shortcut...");
                    await CreateDesktopShortcut(config.InstallPath);
                    OnProgressChanged(65, "Desktop shortcut created");
                }

                if (config.CreateStartMenuShortcut)
                {
                    OnStatusChanged("Creating start menu shortcut...");
                    await CreateStartMenuShortcut(config.InstallPath);
                    OnProgressChanged(75, "Start menu shortcut created");
                }

                // Step 4: Register application
                OnStatusChanged("Registering application...");
                await RegisterApplication(config);
                OnProgressChanged(85, "Application registered");

                // Step 5: Create uninstaller
                OnStatusChanged("Creating uninstaller...");
                await CreateUninstaller(config.InstallPath);
                OnProgressChanged(95, "Uninstaller created");

                // Step 6: Write configuration
                OnStatusChanged("Saving configuration...");
                await WriteConfiguration(config);
                OnProgressChanged(100, "Installation complete");

                OnStatusChanged("Installation completed successfully!");

                return true;
            }
            catch (Exception ex)
            {
                OnStatusChanged($"Installation failed: {ex.Message}");
                return false;
            }
        }

        private bool RequiresAdminPrivileges(string installPath)
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            
            return installPath.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase) ||
                   installPath.StartsWith(programFilesX86, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsRunAsAdmin()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private bool RestartAsAdmin()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    WorkingDirectory = Environment.CurrentDirectory,
                    FileName = Process.GetCurrentProcess().MainModule?.FileName ?? "",
                    Verb = "runas"
                };

                Process.Start(startInfo);
                Environment.Exit(0);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task CreateInstallationDirectory(string installPath)
        {
            await Task.Run(() =>
            {
                if (Directory.Exists(installPath))
                {
                    // Backup existing installation
                    var backupPath = installPath + "_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    Directory.Move(installPath, backupPath);
                }
                Directory.CreateDirectory(installPath);
            });
        }

        private async Task ExtractApplicationFiles(string installPath)
        {
            await Task.Run(async () =>
            {
                // Create a dummy Modrix.exe for demonstration
                // In a real scenario, this would be embedded as a resource or downloaded
                var modrixExePath = Path.Combine(installPath, "Modrix.exe");
                
                // Try to get embedded resource first
                var executingAssembly = Assembly.GetExecutingAssembly();
                var resourceName = "ModrixInstaller.Resources.Modrix.exe";

                using var stream = executingAssembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var fileStream = File.Create(modrixExePath);
                    await stream.CopyToAsync(fileStream);
                }
                else
                {
                    // Create a placeholder executable if resource not found
                    await CreatePlaceholderExecutable(modrixExePath);
                }

                // Copy icon file
                var iconPath = Path.Combine(installPath, "ModrixIcon.ico");
                var sourceIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "ModrixIcon.ico");
                
                if (File.Exists(sourceIconPath))
                {
                    File.Copy(sourceIconPath, iconPath, true);
                }

                // Create additional application files
                await CreateApplicationFiles(installPath);
            });
        }

        private async Task CreatePlaceholderExecutable(string exePath)
        {
            // Create a simple batch file that shows a message (for demonstration)
            var batchContent = @"@echo off
echo Modrix IDE - Minecraft Mod Development Environment
echo This is a placeholder for the actual Modrix application.
echo Installation completed successfully!
pause";

            var batchPath = Path.ChangeExtension(exePath, ".bat");
            await File.WriteAllTextAsync(batchPath, batchContent);

            // Create a simple .exe wrapper (this would be the actual Modrix app in production)
            var exeContent = System.Text.Encoding.UTF8.GetBytes("Modrix Placeholder");
            await File.WriteAllBytesAsync(exePath, exeContent);
        }

        private async Task CreateApplicationFiles(string installPath)
        {
            // Create config directory
            var configDir = Path.Combine(installPath, "config");
            Directory.CreateDirectory(configDir);

            // Create templates directory
            var templatesDir = Path.Combine(installPath, "templates");
            Directory.CreateDirectory(templatesDir);

            // Create sample config file
            var configContent = @"{
  ""version"": ""1.0.0"",
  ""defaultTemplate"": ""fabric-1.20"",
  ""projectsPath"": ""%USERPROFILE%\\ModrixProjects"",
  ""javaPath"": """",
  ""minecraftPath"": ""%APPDATA%\\.minecraft""
}";

            await File.WriteAllTextAsync(Path.Combine(configDir, "settings.json"), configContent);

            // Create README
            var readmeContent = @"# Modrix IDE

Thank you for installing Modrix!

## Getting Started

1. Launch Modrix from your desktop or start menu
2. Create a new project using the project wizard
3. Choose your mod loader (Fabric or Forge)
4. Start developing your amazing Minecraft mods!

## Support

- Documentation: https://github.com/Shlomo1412/Modrix/wiki
- Issues: https://github.com/Shlomo1412/Modrix/issues
- Discord: https://discord.gg/modrix

Happy modding!
";

            await File.WriteAllTextAsync(Path.Combine(installPath, "README.md"), readmeContent);
        }

        private async Task CreateDesktopShortcut(string installPath)
        {
            await Task.Run(() =>
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var shortcutPath = Path.Combine(desktopPath, "Modrix.lnk");
                var targetPath = Path.Combine(installPath, "Modrix.exe");
                
                CreateShortcut(shortcutPath, targetPath, "Modrix - Minecraft Mod Development IDE", installPath);
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
                
                CreateShortcut(shortcutPath, targetPath, "Modrix - Minecraft Mod Development IDE", installPath);

                // Create uninstaller shortcut
                var uninstallShortcutPath = Path.Combine(modrixFolder, "Uninstall Modrix.lnk");
                var uninstallTargetPath = Path.Combine(installPath, "Uninstall.exe");
                CreateShortcut(uninstallShortcutPath, uninstallTargetPath, "Uninstall Modrix", installPath);
            });
        }

        private void CreateShortcut(string shortcutPath, string targetPath, string description, string workingDirectory)
        {
            try
            {
                // Using COM Interop to create shortcuts
                Type t = Type.GetTypeFromCLSID(new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8")); // WshShell
                dynamic shell = Activator.CreateInstance(t);
                var lnk = shell.CreateShortcut(shortcutPath);
                lnk.TargetPath = targetPath;
                lnk.WorkingDirectory = workingDirectory;
                lnk.Description = description;
                lnk.Save();
            }
            catch
            {
                // Fallback: create a simple batch file shortcut
                var batchPath = Path.ChangeExtension(shortcutPath, ".bat");
                var batchContent = $"@echo off\ncd /d \"{workingDirectory}\"\nstart \"\" \"{targetPath}\"";
                File.WriteAllText(batchPath, batchContent);
            }
        }

        private async Task RegisterApplication(InstallationConfiguration config)
        {
            await Task.Run(() =>
            {
                try
                {
                    var registryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Modrix";
                    
                    // Try HKLM first (requires admin)
                    try
                    {
                        using var key = Registry.LocalMachine.CreateSubKey(registryPath);
                        WriteRegistryValues(key, config);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Fallback to HKCU
                        using var key = Registry.CurrentUser.CreateSubKey(registryPath);
                        WriteRegistryValues(key, config);
                    }
                }
                catch (Exception ex)
                {
                    OnStatusChanged($"Warning: Could not register application in registry: {ex.Message}");
                }
            });
        }

        private void WriteRegistryValues(RegistryKey key, InstallationConfiguration config)
        {
            key.SetValue("DisplayName", "Modrix");
            key.SetValue("DisplayVersion", "1.0.0");
            key.SetValue("Publisher", "Modrix Development Team");
            key.SetValue("InstallLocation", config.InstallPath);
            key.SetValue("UninstallString", Path.Combine(config.InstallPath, "Uninstall.exe"));
            key.SetValue("DisplayIcon", Path.Combine(config.InstallPath, "Modrix.exe"));
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
            
            // Estimate installed size (in KB)
            var estimatedSize = GetDirectorySize(config.InstallPath) / 1024;
            key.SetValue("EstimatedSize", estimatedSize, RegistryValueKind.DWord);
        }

        private long GetDirectorySize(string path)
        {
            try
            {
                return new DirectoryInfo(path)
                    .GetFiles("*", SearchOption.AllDirectories)
                    .Sum(file => file.Length);
            }
            catch
            {
                return 150 * 1024 * 1024; // Default 150MB
            }
        }

        private async Task CreateUninstaller(string installPath)
        {
            await Task.Run(() =>
            {
                // Copy this installer as uninstaller with special flag
                var currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                var uninstallerPath = Path.Combine(installPath, "Uninstall.exe");
                
                if (File.Exists(currentExe))
                {
                    File.Copy(currentExe, uninstallerPath, true);
                }
            });
        }

        private async Task WriteConfiguration(InstallationConfiguration config)
        {
            await Task.Run(() =>
            {
                var configPath = Path.Combine(config.InstallPath, "installation.json");
                var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(configPath, json);
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
                    // Give user a chance to close any running processes
                    Thread.Sleep(1000);
                    
                    try
                    {
                        Directory.Delete(installPath, true);
                    }
                    catch
                    {
                        // If we can't delete everything, mark for deletion on reboot
                        MarkDirectoryForDeletion(installPath);
                    }
                }
            });
        }

        private void MarkDirectoryForDeletion(string path)
        {
            try
            {
                // Use Windows API to mark directory for deletion on next reboot
                // This is a simplified implementation
                var tempBatch = Path.GetTempFileName() + ".bat";
                var batchContent = $@"@echo off
timeout /t 3
rd /s /q ""{path}""
del ""%0""";
                
                File.WriteAllText(tempBatch, batchContent);
                Process.Start(new ProcessStartInfo
                {
                    FileName = tempBatch,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
            }
            catch { }
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