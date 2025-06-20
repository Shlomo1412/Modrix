﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Modrix.Models;
using Modrix.Views.Windows;
using Wpf.Ui;

namespace Modrix.Services
{
    public class FabricTemplateManager
    {
        private readonly JdkHelper _jdkHelper = new JdkHelper();
        public async Task FullSetupWithGradle(ModProjectData data, IProgress<(string Message, int Progress)> progress)
        {
            try
            {
                await CopyTemplateFilesAsync(data.Location, progress, data.ModId);
                await FixAssetsFolder(data.Location, data.ModId);
                await FixMixinFiles(data.Location, data.ModId);
                await UpdateModMetadataAsync(data, progress);
                await UpdateBuildFilesAsync(data, progress);
                await UpdateMixinConfigs(data);
                await UpdateModJson(data);
                await CopyIconAsync(data);
                await ChangeModIdInMainClass(data.Location, data.ModId);

                if (data.IncludeReadme)
                {
                    await CreateReadmeFile(data);
                }
                var configPath = Path.Combine(data.Location, "modrix.config");
                await File.WriteAllTextAsync(
                Path.Combine(data.Location, "modrix.config"),
                $"ModId={data.ModId}\n" +
                $"Name={data.Name}\n" +
                $"Package={data.Package}\n" +
                $"ModType=Fabric Mod\n" +
                $"MinecraftVersion={data.MinecraftVersion}\n" +
                $"IconPath=src/main/resources/assets/{data.ModId}/icon.png");

                progress.Report(("Verifying Java environment...", 95));
                await _jdkHelper.EnsureRequiredJdk(data.MinecraftVersion, progress);


                progress.Report(("Project ready!", 100));

                // Show success snackbar
                var navWin = App.Services.GetService<INavigationWindow>();
                if (navWin is MainWindow main)
                    main.ShowProjectCreatedSnackbar(data.Name);

            }
            catch (Exception ex)
            {
                await ShowMessageAsync($"Fabric setup failed: {ex.Message}", "Error");

                var navWin = App.Services.GetService<INavigationWindow>();
                if (navWin is MainWindow main)
                    main.ShowProjectFailedSnackbar(ex.Message);
                throw;
            }
        }

        private async Task CopyTemplateFilesAsync(
            string targetPath,
            IProgress<(string, int)> progress,
            string modId
        )
        {
            var templatePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Templates",
                "FabricMod"
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

                // Replace 'modid' with the real modId in text files
                if (IsTextFile(destPath))
                {
                    var content = await File.ReadAllTextAsync(destPath);
                    
                    // Special handling for Java files
                    if (destPath.EndsWith(".java"))
                    {
                        // Replace other occurrences of modid
                        content = content.Replace("modid", modId);
                    }
                    else if (content.Contains("modid") || content.Contains("MOD_ID"))
                    {
                        content = content.Replace("modid", modId).Replace("MOD_ID", modId.ToUpperInvariant());
                    }
                    
                    await File.WriteAllTextAsync(destPath, content);
                }

                copiedFiles++;
                var currentProgress = 10 + (int)((double)copiedFiles / totalFiles * 25);
                progress.Report(($"Copying files ({copiedFiles}/{totalFiles})", currentProgress));
            }

            var clientResourcesPath = Path.Combine(templatePath, "src", "client", "resources");
            if (Directory.Exists(clientResourcesPath))
            {
                await CopyDirectoryAsync(clientResourcesPath, Path.Combine(targetPath, "src", "client", "resources"));
            }

            await FixAssetsFolder(targetPath, modId);
            await FixMixinFiles(targetPath, modId);
        }

        private bool IsTextFile(string filePath)
        {
            var textExtensions = new[] { ".java", ".json", ".properties", ".gradle", ".md", ".txt", ".xml", ".cfg" };
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return textExtensions.Contains(ext);
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


        private async Task CreateReadmeFile(ModProjectData data)
        {
            var readmePath = Path.Combine(data.Location, "README.md");
            var content = $@"# {data.Name}

            ## Description
                {data.Description ?? "No description provided"}

            ### Mod Details

            - **Mod ID**: `{data.ModId}`

            - **Minecraft Version**: {data.MinecraftVersion}

            - **Mod Type**: {data.ModType}

            - **Authors**: {data.Authors ?? "Not specified"}

            - **License**: {data.License ?? "Not specified"}

            ## Installation
            1. Build the mod using Gradle
            2. Copy the generated JAR file to your mods folder
            ";

            await File.WriteAllTextAsync(readmePath, content);
        }


        private async Task CopyDirectoryAsync(string sourceDir, string destDir)
        {
            foreach (string dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourceDir, destDir));
            }

            foreach (string filePath in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
            {
                await CopyFileAsync(filePath, filePath.Replace(sourceDir, destDir));
            }
        }

        private async Task FixMixinFiles(string projectPath, string modId)
        {
            var mainResourcesPath = Path.Combine(projectPath, "src", "main", "resources");
            var mainMixinFiles = Directory.GetFiles(mainResourcesPath, "modid*.mixins.json");

            
            var clientResourcesPath = Path.Combine(projectPath, "src", "client", "resources");
            var clientMixinFiles = Directory.GetFiles(clientResourcesPath, "modid*.mixins.json");

            foreach (var file in mainMixinFiles.Concat(clientMixinFiles))
            {
                var newName = file.Replace("modid", modId);
                File.Move(file, newName);
            }
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

            using (var sourceStream = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize,
                FileOptions.Asynchronous))

            using (var destStream = new FileStream(
                destPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize,
                FileOptions.Asynchronous))
            {
                await sourceStream.CopyToAsync(destStream);
            }
        }

        private async Task UpdateModMetadataAsync(ModProjectData data, IProgress<(string, int)> progress)
        {
            await Task.Run(async () =>
            {

                progress.Report(("Updating gradle.properties...", 35));
                var gradlePropsPath = Path.Combine(data.Location, "gradle.properties");
                var gradleContent = await File.ReadAllTextAsync(gradlePropsPath);

                
                var originalMcVersion = "1.21.5";
                var fabricVersions = new Dictionary<string, string>
                {
                    { "1.21.4", "0.119.2+1.21.4" },
                    { "1.21.5", "0.119.5+1.21.5" }
                    
                };

                if (!fabricVersions.TryGetValue(data.MinecraftVersion, out var fabricVersion))
                {
                    throw new Exception($"Unsupported Minecraft version: {data.MinecraftVersion}");
                }

                gradleContent = gradleContent
                .Replace($"minecraft_version={originalMcVersion}", $"minecraft_version={data.MinecraftVersion}")
                .Replace($"yarn_mappings={originalMcVersion}+build.1", $"yarn_mappings={data.MinecraftVersion}+build.1")
                .Replace($"fabric_version=0.119.5+{originalMcVersion}", $"fabric_version={fabricVersion}")
                .Replace("mod_version=0.0.1", $"mod_version={data.Version}")
                .Replace("maven_group=com.example", $"maven_group={data.Package}")
                .Replace("archives_base_name=modid", $"archives_base_name={data.ModId}");

                await File.WriteAllTextAsync(gradlePropsPath, gradleContent);

                
                progress.Report(("Renaming main classes...", 50));
                await UpdatePackageStructure(data);
            });
        }

        private async Task UpdatePackageStructure(ModProjectData data)
        {
            try
            {
                var srcPath = Path.Combine(data.Location, "src");
                var packagePath = data.Package.Replace('.', Path.DirectorySeparatorChar);

                
                var allJavaFiles = Directory.GetFiles(srcPath, "*.java", SearchOption.AllDirectories)
                    .Where(f => f.Contains("com" + Path.DirectorySeparatorChar + "example") ||
                              f.Contains("net" + Path.DirectorySeparatorChar + "fabricmc"))
                    .ToList();

                
                var newMainPath = Path.Combine(srcPath, "main", "java", packagePath);
                var newClientPath = Path.Combine(srcPath, "client", "java", packagePath);
                Directory.CreateDirectory(newMainPath);
                Directory.CreateDirectory(newClientPath);

                
                foreach (var filePath in allJavaFiles)
                {
                    var content = await File.ReadAllTextAsync(filePath);

                    // Replace package declaration
                    content = content.Replace("com.example", data.Package)
                                     .Replace("net.fabricmc.example", data.Package);

                    // Replace class name
                    if (filePath.Contains("ExampleMod"))
                    {
                        content = content.Replace("ExampleMod", $"{data.ModId}Mod");
                    }
                    if (filePath.Contains("ExampleModClient"))
                    {
                        content = content.Replace("ExampleModClient", $"{data.ModId}ModClient");
                    }

                    var newPath = filePath
                        .Replace("com" + Path.DirectorySeparatorChar + "example", packagePath)
                        .Replace("net" + Path.DirectorySeparatorChar + "fabricmc" + Path.DirectorySeparatorChar + "example", packagePath)
                        .Replace("ExampleMod", $"{data.ModId}Mod");

                    Directory.CreateDirectory(Path.GetDirectoryName(newPath));
                    await File.WriteAllTextAsync(newPath, content);
                }

                var mainMixinPath = Path.Combine(srcPath, "main", "java", packagePath, "mixin");
                var clientMixinPath = Path.Combine(srcPath, "client", "java", packagePath, "mixin");
                Directory.CreateDirectory(mainMixinPath);
                Directory.CreateDirectory(clientMixinPath);

                
                await MoveMixinsToPackage(
                    Path.Combine(srcPath, "main", "java", "com", "example", "mixin"),
                    mainMixinPath,
                    data.Package + ".mixin",
                    data.ModId
                );

                await MoveMixinsToPackage(
                    Path.Combine(srcPath, "client", "java", "com", "example", "mixin"),
                    clientMixinPath,
                    data.Package + ".mixin.client",
                    data.ModId
                );

                
                await DeleteOldPackages(srcPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error updating package skeleton: {ex.Message}");
            }
        }

        private async Task MoveMixinsToPackage(string sourceDir, string destDir, string package, string modId)
        {
            if (!Directory.Exists(sourceDir)) return;

            var allFiles = Directory.GetFiles(sourceDir, "*.java", SearchOption.AllDirectories);

            foreach (var file in allFiles)
            {
                var content = await File.ReadAllTextAsync(file);
                content = content
                    .Replace("com.example.mixin", package)
                    .Replace("ExampleMixin", $"{modId}Mixin");

                var relativePath = Path.GetRelativePath(sourceDir, file);
                var newPath = Path.Combine(destDir, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(newPath));
                await File.WriteAllTextAsync(newPath, content);
            }

            Directory.Delete(sourceDir, true);
        }

        private async Task DeleteOldPackages(string srcPath)
        {
            var oldPaths = new[]
            {
                Path.Combine(srcPath, "main", "java", "com", "example"),
                Path.Combine(srcPath, "main", "java", "net", "fabricmc", "example"),
                Path.Combine(srcPath, "client", "java", "com", "example"),
                Path.Combine(srcPath, "client", "java", "net", "fabricmc", "example")
            };

            foreach (var path in oldPaths)
            {
                if (Directory.Exists(path))
                {
                    try
                    {
                        Directory.Delete(path, true);
                        
                        CleanEmptyAncestorDirectories(Path.GetDirectoryName(path));
                    }
                    catch
                    {
                        await Task.Delay(500);
                        Directory.Delete(path, true);
                        CleanEmptyAncestorDirectories(Path.GetDirectoryName(path));
                    }
                }
            }
        }

        private async Task UpdateMixinConfigs(ModProjectData data)
        {
            
            var mainResourcesPath = Path.Combine(data.Location, "src", "main", "resources");
            await ProcessMixinFile(mainResourcesPath, data.ModId, data.Package + ".mixin");

            
            var clientResourcesPath = Path.Combine(data.Location, "src", "client", "resources");
            await ProcessMixinFile(clientResourcesPath, data.ModId, data.Package + ".mixin.client");
        }

        private async Task ProcessMixinFile(string resourcesPath, string modId, string package)
        {
            var oldMixinPath = Path.Combine(resourcesPath, "modid.mixins.json");
            var newMixinPath = Path.Combine(resourcesPath, $"{modId}.mixins.json");

            if (File.Exists(oldMixinPath))
            {
                File.Move(oldMixinPath, newMixinPath);
                await UpdateMixinFileContent(newMixinPath, package);
            }
        }

        private async Task UpdateMixinFileContent(string filePath, string package)
        {
            var content = await File.ReadAllTextAsync(filePath);
            content = Regex.Replace(
                content,
                @"""package"":\s*""([^""]*)""",
                $"\"package\": \"{package}\""
            );
            await File.WriteAllTextAsync(filePath, content);
        }

        private async Task UpdateModJson(ModProjectData data)
        {
            var modJsonPath = Path.Combine(
                data.Location,
                "src",
                "main",
                "resources",
                "fabric.mod.json"
            );

            if (!File.Exists(modJsonPath)) return;

            var content = await File.ReadAllTextAsync(modJsonPath);

            
            content = content
                .Replace("\"id\": \"modid\"", $"\"id\": \"{data.ModId}\"")
                .Replace("\"version\": \"1.0.0\"", $"\"version\": \"{data.Version}\"")
                .Replace("\"name\": \"Example mod\"", $"\"name\": \"{data.Name}\"")
                .Replace(
                    "\"description\": \"This is an example description!\"",     
                    $"\"description\": \"{data.Description}\""
                )
                .Replace("\"Me!\"", $"\"{data.Authors}\"")
                .Replace("\"icon\": \"assets/modid/icon.png\"", $"\"icon\": \"assets/{data.ModId}/icon.png\"")
                .Replace("\"com.example.ExampleMod\"", $"\"{data.Package}.{data.ModId}Mod\"")
                .Replace("\"com.example.ExampleModClient\"", $"\"{data.Package}.{data.ModId}ModClient\"")
                .Replace("\"modid.mixins.json\"", $"\"{data.ModId}.mixins.json\"")
                .Replace("\"modid.client.mixins.json\"", $"\"{data.ModId}.client.mixins.json\"")
                .Replace("\"minecraft\": \"~1.21.5\"", $"\"minecraft\": \"~{data.MinecraftVersion}\"");

            
            var authorsArray = $"[{string.Join(", ", data.Authors.Split(',').Select(a => $"\"{a.Trim()}\""))}]";
            content = Regex.Replace(
                content,
                @"""authors"":\s*\[[^\]]*\]",
                $"\"authors\": {authorsArray}"
            );

            await File.WriteAllTextAsync(modJsonPath, content);
        }


        private void CleanEmptyAncestorDirectories(string startPath)
        {
            var currentDir = new DirectoryInfo(startPath);

            while (currentDir != null &&
                  (currentDir.Name == "com" || currentDir.Name == "net") &&
                   currentDir.GetFiles().Length == 0 &&
                   currentDir.GetDirectories().Length == 0)
            {
                var parent = currentDir.Parent;
                currentDir.Delete();
                currentDir = parent;
            }
        }

        private async Task UpdateBuildFilesAsync(ModProjectData data, IProgress<(string, int)> progress)
        {
            await Task.Run(async () =>
            {
                progress.Report(("Updating fabric.mod.json...", 70));
                var modJsonPath = Path.Combine(data.Location, "src", "main", "resources", "fabric.mod.json");
                var modJson = (await File.ReadAllTextAsync(modJsonPath))
                    .Replace("\"id\": \"example-mod\"", $"\"id\": \"{data.ModId}\"")
                    .Replace("\"name\": \"Example Mod\"", $"\"name\": \"{data.Name}\"")
                    .Replace("\"version\": \"${version}\"", $"\"version\": \"{data.Version}\"")
                    .Replace("net.fabricmc.example", data.Package);

                await File.WriteAllTextAsync(modJsonPath, modJson);
            });
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

        /// <summary>
        /// Finds the main class file (src/main/java/.../{modId}Mod.java) and replaces the MOD_ID constant value with the correct modId.
        /// </summary>
        public async Task ChangeModIdInMainClass(string projectPath, string modId)
        {
            // Build the expected main class file path
            var mainJavaDir = Path.Combine(projectPath, "src", "main", "java");
            if (!Directory.Exists(mainJavaDir)) return;

            // Recursively search for the main class file named {modId}Mod.java (case-insensitive)
            var mainClassFile = Directory.GetFiles(mainJavaDir, "*Mod.java", SearchOption.AllDirectories)
                .FirstOrDefault(f => string.Equals(Path.GetFileName(f), $"{modId}Mod.java", StringComparison.OrdinalIgnoreCase));

            if (mainClassFile == null || !File.Exists(mainClassFile)) return;

            var content = await File.ReadAllTextAsync(mainClassFile);
            var pattern = "public static final String MOD_ID = \"modid\";";
            var replacement = $"public static final String MOD_ID = \"{modId}\";";

            if (content.Contains(pattern))
            {
                content = content.Replace(pattern, replacement);
                await File.WriteAllTextAsync(mainClassFile, content);
            }
        }

        private async Task ShowMessageAsync(string message, string title)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            });
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

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destPath));

                using (var sourceStream = File.OpenRead(data.IconPath))
                using (var destStream = File.Create(destPath))
                {
                    await sourceStream.CopyToAsync(destStream);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to copy icon: {ex.Message}");
            }
        }
    }
}