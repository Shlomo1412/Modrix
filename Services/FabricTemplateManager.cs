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
            {
                await ShowMessageAsync($"Template not found at: {templatePath}", "Missing Template");
                throw new DirectoryNotFoundException($"Template directory missing: {templatePath}");
            }

            if (Directory.Exists(targetPath))
            {
                await RetryDeleteDirectoryAsync(targetPath);
            }

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
                progress.Report(("Updating gradle properties...", 35));
                var gradlePropsPath = Path.Combine(data.Location, "gradle.properties");
                var gradleProps = (await File.ReadAllTextAsync(gradlePropsPath))
                    .Replace("mod_version=0.0.1", $"mod_version={data.Version}")
                    .Replace("maven_group=com.example", $"maven_group={data.Package}")
                    .Replace("archives_base_name=modid", $"archives_base_name={data.ModId}");

                await File.WriteAllTextAsync(gradlePropsPath, gradleProps);

                progress.Report(("Updating main class...", 50));
                var packagePath = data.Package.Replace('.', Path.DirectorySeparatorChar);
                var srcPath = Path.Combine(data.Location, "src", "main", "java", packagePath);
                var examplePath = Path.Combine(data.Location, "src", "main", "java", "net", "fabricmc", "example");

                if (Directory.Exists(examplePath))
                {
                    Directory.Move(examplePath, srcPath);
                }

                var modFile = Path.Combine(srcPath, "ExampleMod.java");
                if (File.Exists(modFile))
                {
                    var newFileName = Path.Combine(srcPath, $"{data.ModId}Mod.java");
                    File.Move(modFile, newFileName);

                    var content = (await File.ReadAllTextAsync(newFileName))
                        .Replace("net.fabricmc.example", data.Package)
                        .Replace("ExampleMod", $"{SanitizeClassName(data.Name)}Mod");

                    await File.WriteAllTextAsync(newFileName, content);
                }
            });
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

        private string SanitizeClassName(string name)
        {
            return new string(name
                .Where(c => char.IsLetterOrDigit(c) || c == '_')
                .ToArray());
        }
    }
}