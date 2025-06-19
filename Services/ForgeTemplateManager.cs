using System;
using System.Collections.Generic;
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

        private static readonly Dictionary<string, (string forgeVersion, string loaderVersion)> ForgeVersions = new()
        {
            { "1.21.4", ("54.1.3", "54") },
            { "1.21.5", ("55.0.22", "55") }
        };

        public async Task FullSetupWithGradle(ModProjectData data, IProgress<(string Message, int Progress)> progress)
        {
            try
            {
                // Initialize project structure
                data.ProjectDir = data.Location;
                data.SrcDir = Path.Combine(data.ProjectDir, "src", "main");
                data.JavaDir = Path.Combine(data.SrcDir, "java");
                data.ResourcesDir = Path.Combine(data.SrcDir, "resources");
                data.PackageDir = Path.Combine(data.JavaDir, data.Package.Replace('.', Path.DirectorySeparatorChar));

                // Ensure base directories exist
                Directory.CreateDirectory(data.ProjectDir);
                Directory.CreateDirectory(data.JavaDir);
                Directory.CreateDirectory(data.ResourcesDir);
                Directory.CreateDirectory(data.PackageDir);
                Directory.CreateDirectory(Path.Combine(data.ResourcesDir, "META-INF"));

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

                progress.Report(("Project created successfully!", 100));

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

            // Get the appropriate Forge version for the Minecraft version
            if (!ForgeVersions.TryGetValue(data.MinecraftVersion, out var forgeInfo))
            {
                throw new Exception($"Unsupported Minecraft version: {data.MinecraftVersion}. Supported versions are: {string.Join(", ", ForgeVersions.Keys)}");
            }

            // Replace minecraft version and related properties
            gradleContent = gradleContent
                .Replace("minecraft_version=1.21.5", $"minecraft_version={data.MinecraftVersion}")
                .Replace("minecraft_version_range=[1.21.5,1.22)", $"minecraft_version_range=[{data.MinecraftVersion},1.22)")
                .Replace("mapping_version=1.21.5", $"mapping_version={data.MinecraftVersion}")
                .Replace("forge_version=55.0.22", $"forge_version={forgeInfo.forgeVersion}")
                .Replace("forge_version_range=[55,)", $"forge_version_range=[{forgeInfo.loaderVersion},)")
                .Replace("loader_version_range=[55,)", $"loader_version_range=[{forgeInfo.loaderVersion},)");

            // Replace mod properties
            gradleContent = gradleContent
                .Replace("mod_id=example", $"mod_id={data.ModId}")
                .Replace("mod_name=Example", $"mod_name={data.Name}")
                .Replace("mod_license=Apache 2.0", $"mod_license={data.License}")
                .Replace("mod_version=1.0.0", $"mod_version={data.Version}")
                .Replace("mod_group_id=com.example", $"mod_group_id={data.Package}")
                .Replace("mod_authors=YourName", $"mod_authors={data.Authors}")
                .Replace("mod_description=A simple example mod that demonstrates Forge setup.", $"mod_description={data.Description}");

            await File.WriteAllTextAsync(gradlePropsPath, gradleContent);

            // Update settings.gradle
            var settingsPath = Path.Combine(data.Location, "settings.gradle");
            var settingsContent = await File.ReadAllTextAsync(settingsPath);
            settingsContent = settingsContent.Replace("rootProject.name = 'example'", $"rootProject.name = '{data.ModId}'");
            await File.WriteAllTextAsync(settingsPath, settingsContent);

            // Update mixins.json
            var oldMixinsPath = Path.Combine(data.Location, "src", "main", "resources", "example.mixins.json");
            var newMixinsPath = Path.Combine(data.Location, "src", "main", "resources", $"{data.ModId}.mixins.json");
            if (File.Exists(oldMixinsPath))
            {
                var mixinsContent = await File.ReadAllTextAsync(oldMixinsPath);
                mixinsContent = mixinsContent
                    .Replace("\"package\": \"com.example.mixin\"", $"\"package\": \"{data.Package}.mixin\"")
                    .Replace("\"compatibilityLevel\": \"JAVA_8\"", "\"compatibilityLevel\": \"JAVA_17\"")
                    .Replace("\"refmap\": \"example.refmap.json\"", $"\"refmap\": \"{data.ModId}.refmap.json\"");

                await File.WriteAllTextAsync(newMixinsPath, mixinsContent);
                if (oldMixinsPath != newMixinsPath)
                    File.Delete(oldMixinsPath);
            }

            // Update pack.mcmeta
            var mcmetaPath = Path.Combine(data.Location, "src", "main", "resources", "pack.mcmeta");
            if (File.Exists(mcmetaPath))
            {
                var mcmetaContent = await File.ReadAllTextAsync(mcmetaPath);
                mcmetaContent = mcmetaContent.Replace("\"description\": \"example resources\"", $"\"description\": \"{data.ModId} resources\"");
                await File.WriteAllTextAsync(mcmetaPath, mcmetaContent);
            }

            progress.Report(("Updating package structure...", 50));
            await UpdatePackageStructure(data);
        }

        private async Task UpdatePackageStructure(ModProjectData data)
        {
            // Ensure all required directories exist
            var srcPath = Path.Combine(data.Location, "src");
            var packagePath = data.Package.Replace('.', Path.DirectorySeparatorChar);
            var mainPath = Path.Combine(srcPath, "main", "java", packagePath);
            var oldPackagePath = Path.Combine(srcPath, "main", "java", "com", "example");

            // Create the package directory structure
            Directory.CreateDirectory(mainPath);

            // Move and update main mod class and other files
            var filesToUpdate = Directory.GetFiles(oldPackagePath, "*.java", SearchOption.AllDirectories);
            foreach (var oldFile in filesToUpdate)
            {
                var fileName = Path.GetFileName(oldFile);
                var newFileName = fileName;

                // Rename Example.java and ExampleMod.java to {ModId}Mod.java
                if (fileName == "Example.java" || fileName == "ExampleMod.java")
                {
                    newFileName = $"{char.ToUpper(data.ModId[0]) + data.ModId.Substring(1)}Mod.java";
                }

                var content = await File.ReadAllTextAsync(oldFile);

                // Update package declaration
                content = content.Replace("package com.example", $"package {data.Package}");

                // Update mod class name and MODID -> MOD_ID
                content = content
                    .Replace("class Example", $"class {char.ToUpper(data.ModId[0]) + data.ModId.Substring(1)}Mod")
                    .Replace("class ExampleMod", $"class {char.ToUpper(data.ModId[0]) + data.ModId.Substring(1)}Mod")
                    .Replace("@Mod(Example.MODID)", $"@Mod({char.ToUpper(data.ModId[0]) + data.ModId.Substring(1)}Mod.MOD_ID)")
                    .Replace("@Mod(ExampleMod.MODID)", $"@Mod({char.ToUpper(data.ModId[0]) + data.ModId.Substring(1)}Mod.MOD_ID)")
                    .Replace("public static final String MODID = \"example\"", $"public static final String MOD_ID = \"{data.ModId}\"")
                    .Replace("MODID", "MOD_ID")
                    .Replace("modid = MODID", "modid = MOD_ID")
                    .Replace("example:example_block", $"{data.ModId}:{data.ModId}_block")
                    .Replace("example:example_item", $"{data.ModId}:{data.ModId}_item")
                    .Replace("example:example_tab", $"{data.ModId}:{data.ModId}_tab")
                    .Replace("EXAMPLE_BLOCK", $"{data.ModId.ToUpper()}_BLOCK")
                    .Replace("EXAMPLE_ITEM", $"{data.ModId.ToUpper()}_ITEM")
                    .Replace("EXAMPLE_TAB", $"{data.ModId.ToUpper()}_TAB")
                    .Replace("example_block", $"{data.ModId}_block")
                    .Replace("example_item", $"{data.ModId}_item")
                    .Replace("example_tab", $"{data.ModId}_tab")
                    .Replace("public Example(FMLJavaModLoadingContext context)", $"public {char.ToUpper(data.ModId[0]) + data.ModId.Substring(1)}Mod(FMLJavaModLoadingContext context)")
                    .Replace("@Mod.EventBusSubscriber(modid = MOD_ID", "@Mod.EventBusSubscriber(modid = MOD_ID");

                var newFilePath = Path.Combine(mainPath, newFileName);
                Directory.CreateDirectory(Path.GetDirectoryName(newFilePath));
                await File.WriteAllTextAsync(newFilePath, content);
            }

            // Clean up old package structure
            try
            {
                if (Directory.Exists(oldPackagePath))
                {
                    Directory.Delete(oldPackagePath, true);
                    CleanEmptyAncestorDirectories(Path.GetDirectoryName(oldPackagePath));
                }
            }
            catch (Exception)
            {
                // Ignore cleanup errors as they don't affect functionality
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
                settingsContent = settingsContent.Replace("rootProject.name = 'example'", $"rootProject.name = '{data.ModId}'");
                await File.WriteAllTextAsync(settingsPath, settingsContent);
            }

            // Update build.gradle
            var buildGradlePath = Path.Combine(data.Location, "build.gradle");
            if (File.Exists(buildGradlePath))
            {
                var buildContent = await File.ReadAllTextAsync(buildGradlePath);

                // Update mixin configuration
                buildContent = buildContent
                    .Replace("${mod_id}.refmap.json", $"{data.ModId}.refmap.json")
                    .Replace("\"${mod_id}.mixins.json\"", $"\"{data.ModId}.mixins.json\"");

                // Update run configurations
                buildContent = buildContent
                    .Replace("property 'forge.enabledGameTestNamespaces', mod_id", $"property 'forge.enabledGameTestNamespaces', '{data.ModId}'")
                    .Replace("args '--mod', mod_id,", $"args '--mod', '{data.ModId}',");

                await File.WriteAllTextAsync(buildGradlePath, buildContent);
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
                   fileName == "Config.java" ||
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
