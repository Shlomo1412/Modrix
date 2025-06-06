using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Modrix.Models;
using Modrix.Views.Windows;
using Wpf.Ui;

namespace Modrix.Services
{
    public class ForgeTemplateManager
    {
        private readonly JdkHelper _jdkHelper = new JdkHelper();
        private const string ForgeVersion = "54.1.3";

        public async Task FullSetupWithGradle(ModProjectData data, IProgress<(string Message, int Progress)> progress)
        {
            try
            {
                await CopyTemplateFilesAsync(data.Location, progress, data.ModId);
                await FixAssetsFolder(data.Location, data.ModId);
                await UpdateModMetadataAsync(data, progress);
                await UpdateBuildFilesAsync(data, progress);
                await UpdateModToml(data);
                await CopyIconAsync(data);
                
                if (data.IncludeReadme)
                {
                    await CreateReadmeFile(data);
                }
                
                await File.WriteAllTextAsync(
                    Path.Combine(data.Location, "modrix.config"),
                    $"ModId={data.ModId}\n" +
                    $"Name={data.Name}\n" +
                    $"Package={data.Package}\n" +
                    $"ModType=Forge Mod\n" +
                    $"MinecraftVersion={data.MinecraftVersion}\n" +
                    $"IconPath=src/main/resources/assets/{data.ModId}/icon.png");

                progress.Report(("Verifying Java environment...", 95));
                await _jdkHelper.EnsureRequiredJdk(data.MinecraftVersion, progress);
                
                // Run Gradle setup
                await RunGradleSetup(data.Location, progress);

                progress.Report(("Project ready!", 100));

                // Show success snackbar
                var navWin = App.Services.GetService<INavigationWindow>();
                if (navWin is MainWindow main)
                    main.ShowProjectCreatedSnackbar(data.Name);
            }
            catch (Exception ex)
            {
                await ShowMessageAsync($"Forge setup failed: {ex.Message}", "Error");

                var navWin = App.Services.GetService<INavigationWindow>();
                if (navWin is MainWindow main)
                    main.ShowProjectFailedSnackbar(ex.Message);
                throw;
            }
        }

        private async Task CopyTemplateFilesAsync(string targetPath, IProgress<(string, int)> progress, string modId)
        {
            var templatePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Templates",
                "ForgeMod"
            );

            if (!Directory.Exists(templatePath))
                throw new DirectoryNotFoundException($"Template directory missing: {templatePath}");

            if (Directory.Exists(targetPath))
                await RetryDeleteDirectoryAsync(targetPath);

            Directory.CreateDirectory(targetPath);

            var allFiles = Directory.GetFiles(templatePath, "*.*", SearchOption.AllDirectories);
            var totalFiles = allFiles.Length;
            var copiedFiles = 0;

            foreach (var filePath in allFiles)
            {
                if (ShouldSkipFile(filePath)) continue;

                var relativePath = Path.GetRelativePath(templatePath, filePath);
                var destPath = Path.Combine(targetPath, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath));

                await CopyFileAsync(filePath, destPath);

                copiedFiles++;
                var currentProgress = 10 + (int)((double)copiedFiles / totalFiles * 25);
                progress.Report(($"Copying files ({copiedFiles}/{totalFiles})", currentProgress));
            }
        }

        private async Task FixAssetsFolder(string projectPath, string modId)
        {
            var oldAssetsPath = Path.Combine(projectPath, "src", "main", "resources", "assets", "modid");
            var newAssetsPath = Path.Combine(projectPath, "src", "main", "resources", "assets", modId);

            if (Directory.Exists(oldAssetsPath))
            {
                Directory.Move(oldAssetsPath, newAssetsPath);
            }
        }

        private async Task UpdateModMetadataAsync(ModProjectData data, IProgress<(string, int)> progress)
        {
            progress.Report(("Updating gradle.properties...", 35));
            var gradlePropsPath = Path.Combine(data.Location, "gradle.properties");
            var gradleContent = await File.ReadAllTextAsync(gradlePropsPath);

            gradleContent = gradleContent
                .Replace("mod_version=1.0.0", $"mod_version={data.Version}")
                .Replace("mod_group_id=com.example", $"mod_group_id={data.Package}")
                .Replace("mod_id=examplemod", $"mod_id={data.ModId}")
                .Replace("mod_name=Example Mod", $"mod_name={data.Name}")
                .Replace("mod_license=All Rights Reserved", $"mod_license={data.License}")
                .Replace("mod_version=1.0.0", $"mod_version={data.ModVersion}")
                .Replace("mod_authors=Author Name", $"mod_authors={data.Authors}")
                .Replace("mod_description=Example mod description", $"mod_description={data.Description}");

            await File.WriteAllTextAsync(gradlePropsPath, gradleContent);

            progress.Report(("Updating package structure...", 50));
            await UpdatePackageStructure(data);
        }

        private async Task UpdatePackageStructure(ModProjectData data)
        {
            var srcPath = Path.Combine(data.Location, "src");
            var packagePath = data.Package.Replace('.', Path.DirectorySeparatorChar);
            var mainPath = Path.Combine(srcPath, "main", "java", packagePath);

            Directory.CreateDirectory(mainPath);

            // Move and update main mod class
            var oldMainClass = Path.Combine(srcPath, "main", "java", "com", "example", "ExampleMod.java");
            if (File.Exists(oldMainClass))
            {
                var content = await File.ReadAllTextAsync(oldMainClass);
                content = content
                    .Replace("com.example", data.Package)
                    .Replace("ExampleMod", $"{data.ModId}Mod");

                var newMainClass = Path.Combine(mainPath, $"{data.ModId}Mod.java");
                await File.WriteAllTextAsync(newMainClass, content);
            }

            // Clean up old package structure
            var oldPackagePath = Path.Combine(srcPath, "main", "java", "com", "example");
            if (Directory.Exists(oldPackagePath))
            {
                Directory.Delete(oldPackagePath, true);
                CleanEmptyAncestorDirectories(oldPackagePath);
            }
        }

        private async Task UpdateBuildFilesAsync(ModProjectData data, IProgress<(string, int)> progress)
        {
            progress.Report(("Updating build files...", 70));

            // Update settings.gradle
            var settingsPath = Path.Combine(data.Location, "settings.gradle");
            if (File.Exists(settingsPath))
            {
                var settingsContent = await File.ReadAllTextAsync(settingsPath);
                settingsContent = settingsContent.Replace("rootProject.name = 'examplemod'", $"rootProject.name = '{data.ModId}'");
                await File.WriteAllTextAsync(settingsPath, settingsContent);
            }
        }

        private async Task UpdateModToml(ModProjectData data)
        {
            var modTomlPath = Path.Combine(data.Location, "src", "main", "resources", "META-INF", "mods.toml");
            if (!File.Exists(modTomlPath)) return;

            var content = await File.ReadAllTextAsync(modTomlPath);
            content = content
                .Replace("modid=\"examplemod\"", $"modid=\"{data.ModId}\"")
                .Replace("version=\"${version}\"", $"version=\"{data.Version}\"")
                .Replace("displayName=\"Example Mod\"", $"displayName=\"{data.Name}\"")
                .Replace("authors=\"Example Author\"", $"authors=\"{data.Authors}\"")
                .Replace("description='''\\nThis is an example description!\\n'''", $"description='''\\n{data.Description}\\n'''");

            await File.WriteAllTextAsync(modTomlPath, content);
        }

        private async Task RunGradleSetup(string projectPath, IProgress<(string, int)> progress)
        {
            string gradlewPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                ? Path.Combine(projectPath, "gradlew.bat")
                : Path.Combine(projectPath, "gradlew");

            if (!File.Exists(gradlewPath))
                throw new FileNotFoundException("Gradle wrapper not found");

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var chmod = new ProcessStartInfo
                {
                    FileName = "/bin/chmod",
                    Arguments = $"+x {gradlewPath}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(chmod)?.WaitForExit();
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = gradlewPath,
                Arguments = "genIntellijRuns",
                WorkingDirectory = projectPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new Exception("Gradle setup failed");
        }

        private async Task CreateReadmeFile(ModProjectData data)
        {
            var readmePath = Path.Combine(data.Location, "README.md");
            var content = $@"# {data.Name}

## Description
{data.Description ?? "No description provided"}

### Mod Details
- **Mod ID**: `{data.ModId}`
- **Minecraft Version**: {data.MinecraftVersion}
- **Mod Type**: Forge Mod
- **Authors**: {data.Authors ?? "Not specified"}
- **License**: {data.License ?? "Not specified"}

## Installation
1. Install Forge for Minecraft {data.MinecraftVersion}
2. Place the mod JAR file in your mods folder
3. Launch Minecraft with the Forge profile
";

            await File.WriteAllTextAsync(readmePath, content);
        }

        private async Task CopyIconAsync(ModProjectData data)
        {
            if (string.IsNullOrEmpty(data.IconPath)) return;

            var destPath = Path.Combine(
                data.Location,
                "src",
                "main",
                "resources",
                "assets",
                data.ModId,
                "icon.png"
            );

            Directory.CreateDirectory(Path.GetDirectoryName(destPath));
            await CopyFileAsync(data.IconPath, destPath);
        }

        private bool ShouldSkipFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            return fileName.EndsWith(".idx") ||
                   fileName == ".git" ||
                   fileName == ".gitignore" ||
                   fileName == ".gitattributes" ||
                   fileName.Equals("LICENSE", StringComparison.OrdinalIgnoreCase) ||
                   fileName == "README.md";
        }

        private async Task RetryDeleteDirectoryAsync(string path)
        {
            const int maxRetries = 3;
            const int delayMs = 300;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, true);
                    }
                    return;
                }
                catch
                {
                    if (i == maxRetries - 1) throw;
                    await Task.Delay(delayMs);
                }
            }
        }

        private async Task CopyFileAsync(string sourcePath, string destPath)
        {
            const int bufferSize = 4096;

            using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.Asynchronous);
            using var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous);
            await sourceStream.CopyToAsync(destStream);
        }

        private void CleanEmptyAncestorDirectories(string startPath)
        {
            var currentDir = new DirectoryInfo(startPath);
            while (currentDir != null && 
                   (currentDir.Name == "com" || currentDir.Name == "example") && 
                   !currentDir.GetFiles().Any() && 
                   !currentDir.GetDirectories().Any())
            {
                var parent = currentDir.Parent;
                currentDir.Delete();
                currentDir = parent;
            }
        }

        private async Task ShowMessageAsync(string message, string title)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }
    }
}
