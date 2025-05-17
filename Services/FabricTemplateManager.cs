using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Modrix.Models;

namespace Modrix.Services
{
    public class FabricTemplateManager
    {
        public async Task FullSetupWithGradle(ModProjectData data, IProgress<(string Message, int Progress)> progress)
        {
            try
            {
                await CopyTemplateFilesAsync(data.Location, progress);
                await UpdateModMetadataAsync(data, progress);
                await UpdateBuildFilesAsync(data, progress);
                await CopyIconAsync(data);
                await UpdateMixinConfigs(data);
                progress.Report(("Project ready!", 100));
            }
            catch (Exception ex)
            {
                await ShowMessageAsync($"Fabric setup failed: {ex.Message}", "Error");
                throw;
            }
        }

        private async Task CopyTemplateFilesAsync(string targetPath, IProgress<(string, int)> progress)
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

                copiedFiles++;
                var currentProgress = 10 + (int)((double)copiedFiles / totalFiles * 25);
                progress.Report(($"Copying files ({copiedFiles}/{totalFiles})", currentProgress));
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
                // עדכון gradle.properties
                progress.Report(("Updating gradle.properties...", 35));
                var gradlePropsPath = Path.Combine(data.Location, "gradle.properties");
                var gradleContent = await File.ReadAllTextAsync(gradlePropsPath);

                gradleContent = gradleContent
                    .Replace("mod_version=0.0.1", $"mod_version={data.Version}")
                    .Replace("maven_group=com.example", $"maven_group={data.Package}")
                    .Replace("archives_base_name=modid", $"archives_base_name={data.ModId}")
                    .Replace("minecraft_version=1.21.4", $"minecraft_version={data.MinecraftVersion}");

                await File.WriteAllTextAsync(gradlePropsPath, gradleContent);

                // שינוי שם הקבצים והחבילה
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

                // שלב 2: יצירת מבנה התיקיות החדש
                var newMainPath = Path.Combine(srcPath, "main", "java", packagePath);
                var newClientPath = Path.Combine(srcPath, "client", "java", packagePath);
                Directory.CreateDirectory(newMainPath);
                Directory.CreateDirectory(newClientPath);

                // שלב 3: העברה ועדכון קבצים
                foreach (var filePath in allJavaFiles)
                {
                    var content = await File.ReadAllTextAsync(filePath);

                    // החלפת package
                    content = content.Replace("com.example", data.Package)
                                     .Replace("net.fabricmc.example", data.Package);

                    // החלפת שמות מחלקות
                    if (filePath.Contains("ExampleMod"))
                    {
                        content = content.Replace("ExampleMod", $"{data.ModId}Mod");
                    }
                    if (filePath.Contains("ExampleModClient"))
                    {
                        content = content.Replace("ExampleModClient", $"{data.ModId}ModClient");
                    }

                    // קביעת מיקום חדש
                    var newPath = filePath
                        .Replace("com" + Path.DirectorySeparatorChar + "example", packagePath)
                        .Replace("net" + Path.DirectorySeparatorChar + "fabricmc" + Path.DirectorySeparatorChar + "example", packagePath)
                        .Replace("ExampleMod", $"{data.ModId}Mod");

                    Directory.CreateDirectory(Path.GetDirectoryName(newPath));
                    await File.WriteAllTextAsync(newPath, content);
                }

                // שלב 4: מחיקת תיקיות מקור
                await DeleteOldPackages(srcPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"שגיאה בעדכון מבנה החבילה: {ex.Message}");
            }
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
            var mixinConfigPath = Path.Combine(
                data.Location,
                "src",
                "main",
                "resources",
                $"{data.ModId}.mixins.json"
            );

            var clientMixinConfigPath = Path.Combine(
                data.Location,
                "src",
                "main",
                "resources",
                $"{data.ModId}.client.mixins.json"
            );

            // עדכון קובץ המיקסין הראשי
            if (File.Exists(mixinConfigPath))
            {
                var content = await File.ReadAllTextAsync(mixinConfigPath);
                content = content
                    .Replace("\"package\": \"com.example.mixin\"",
                            $"\"package\": \"{data.Package}.mixin\"");

                await File.WriteAllTextAsync(mixinConfigPath, content);
            }

            // עדכון קובץ המיקסין של הקליינט
            if (File.Exists(clientMixinConfigPath))
            {
                var content = await File.ReadAllTextAsync(clientMixinConfigPath);
                content = content
                    .Replace("\"package\": \"com.example.mixin.client\"",
                            $"\"package\": \"{data.Package}.mixin.client\"");

                await File.WriteAllTextAsync(clientMixinConfigPath, content);
            }
        }

        private string FindExampleRoot(string srcPath)
        {
            // חיפוש רקורסיבי אחר התיקייה המכילה את דוגמת הקוד
            var directories = Directory.GetDirectories(
                Path.Combine(srcPath, "main", "java"),
                "*",
                SearchOption.AllDirectories
            );

            foreach (var dir in directories)
            {
                if (dir.EndsWith("com" + Path.DirectorySeparatorChar + "example") ||
                    dir.EndsWith("net" + Path.DirectorySeparatorChar + "fabricmc" + Path.DirectorySeparatorChar + "example"))
                {
                    return Directory.GetParent(dir).FullName;
                }
            }
            return null;
        }

        private async Task MoveDirectoryContentsAsync(string sourceDir, string targetDir, string package, string modId)
        {
            if (!Directory.Exists(sourceDir)) return;

            var allFiles = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);

            foreach (var filePath in allFiles)
            {
                var relativePath = Path.GetRelativePath(sourceDir, filePath);
                var newPath = Path.Combine(targetDir, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(newPath));

                // שינוי תוכן הקובץ
                var content = await File.ReadAllTextAsync(filePath);
                content = content
                    .Replace("com.example", package)
                    .Replace("net.fabricmc.example", package)
                    .Replace("ExampleMod", $"{modId}Mod");

                await File.WriteAllTextAsync(newPath, content);

                // מחיקת הקובץ המקורי
                File.Delete(filePath);
            }
        }

        private async Task UpdateAllJavaFiles(ModProjectData data)
        {
            // עדכון קבצי main
            await ProcessJavaFiles(
                Path.Combine(data.Location, "src", "main", "java"),
                data.Package,
                data.ModId,
                new[] { "ExampleMod", "ExampleMixin" }
            );

            // עדכון קבצי client
            await ProcessJavaFiles(
                Path.Combine(data.Location, "src", "client", "java"),
                data.Package,
                data.ModId,
                new[] { "ExampleModClient" }
            );
        }

        private async Task ProcessJavaFiles(string basePath, string package, string modId, string[] patterns)
        {
            if (!Directory.Exists(basePath)) return;

            var allFiles = Directory.GetFiles(basePath, "*.java", SearchOption.AllDirectories);

            foreach (var file in allFiles)
            {
                var content = await File.ReadAllTextAsync(file);
                var newContent = content
                    .Replace("com.example", package)
                    .Replace("net.fabricmc.example", package);

                foreach (var pattern in patterns)
                {
                    newContent = newContent.Replace(pattern, $"{modId}{pattern.Replace("Example", "")}");
                }

                var newFileName = Path.GetFileName(file);
                foreach (var pattern in patterns)
                {
                    newFileName = newFileName.Replace(pattern, $"{modId}{pattern.Replace("Example", "")}");
                }

                var newPath = Path.Combine(Path.GetDirectoryName(file), newFileName);

                await File.WriteAllTextAsync(newPath, newContent);
                if (file != newPath) File.Delete(file);
            }
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
                   fileName == ".gitattributes";
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
                "icon.png"
            );

            try
            {
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