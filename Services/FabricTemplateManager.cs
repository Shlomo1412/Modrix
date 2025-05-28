using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Modrix.Models;

namespace Modrix.Services
{
    public class FabricTemplateManager
    {

        private const string GradleWrapperUnix = "gradlew";
        private const string GradleWrapperWin = "gradlew.bat";
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

                progress.Report(("Running Gradle build...", 95));
                await RunGradleAsync(data.Location, "build", progress, data.MinecraftVersion);

                progress.Report(("Project ready!", 100));
            }
            catch (Exception ex)
            {
                await ShowMessageAsync($"Fabric setup failed: {ex.Message}", "Error");
                throw;
            }
        }

        private int GetRequiredJavaVersion(string minecraftVersion)
        {
            if (string.IsNullOrEmpty(minecraftVersion)) return 17;

            // Parse version (e.g., "1.21.4")
            var parts = minecraftVersion.Split('.');
            if (parts.Length < 2) return 17;

            if (!int.TryParse(parts[1], out int minor)) return 17;

            // Version mapping:
            return minor >= 21 ? 21 :
                   minor >= 17 ? 17 : 8;
        }

        private async Task<bool> ShowDownloadDialogAsync(int requiredVersion)
        {
            return await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var result = MessageBox.Show(
                    $"This project requires Java {requiredVersion} which is not installed.\n\n" +
                    "Would you like to download and install it automatically?",
                    "Java JDK Required",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );

                return result == MessageBoxResult.Yes;
            });
        }

        private async Task<string> DownloadAndInstallJdkAsync(int version, IProgress<(string, int)> progress)
        {
            try
            {
                progress.Report(($"Downloading JDK {version}...", 95));

                // Create persistent directory for JDK
                var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var jdkRoot = Path.Combine(appDataDir, "Modrix", "JDKs");
                Directory.CreateDirectory(jdkRoot);

                // Download JDK
                var jdkUrl = version switch
                {
                    21 => "https://mirrors.huaweicloud.com/openjdk/21.0.2/openjdk-21.0.2_windows-x64_bin.zip",
                    17 => "https://mirrors.huaweicloud.com/openjdk/17.0.10/openjdk-17.0.10_windows-x64_bin.zip",
                    _ => throw new NotSupportedException($"Unsupported JDK version: {version}")
                };

                var zipPath = Path.Combine(jdkRoot, $"jdk-{version}.zip");
                using (var client = new WebClient())
                {
                    client.DownloadProgressChanged += (s, e) =>
                    {
                        progress.Report(($"Downloading JDK {version}... {e.ProgressPercentage}%", 95));
                    };

                    await client.DownloadFileTaskAsync(jdkUrl, zipPath);
                }

                progress.Report(($"Installing JDK {version}...", 96));

                // Extract JDK
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, jdkRoot);
                File.Delete(zipPath); // Clean up zip file

                // Find the extracted JDK directory
                var jdkDir = Directory.GetDirectories(jdkRoot)
                    .FirstOrDefault(d => d.Contains($"jdk-{version}"));

                if (jdkDir == null)
                    throw new Exception("Failed to find JDK in downloaded package");

                progress.Report(($"JDK {version} installed successfully!", 97));
                return jdkDir;
            }
            catch (Exception ex)
            {
                await ShowMessageAsync($"JDK installation failed: {ex.Message}", "Error");
                return null;
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

                    
                    content = content.Replace("com.example", data.Package)
                                     .Replace("net.fabricmc.example", data.Package);

                    
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

        private async Task RunGradleAsync(
    string projectDir,
    string gradleTasks,
    IProgress<(string Message, int Progress)> progress, string minecraftVersion)
        {
            var wrapper = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "gradlew.bat" : "gradlew";
            
            var wrapperPath = Path.Combine(projectDir, wrapper);
            if (!File.Exists(wrapperPath))
                throw new FileNotFoundException($"Cannot find {wrapper} in {projectDir}");

            int requiredJava = GetRequiredJavaVersion(minecraftVersion);
            var jdkHome = FindBestJdkHome(requiredJava);

            if (jdkHome == null)
            {
                // Prompt user to download JDK
                var result = await ShowDownloadDialogAsync(requiredJava);
                if (result)
                {
                    jdkHome = await DownloadAndInstallJdkAsync(requiredJava, progress);
                    if (jdkHome == null)
                    {
                        throw new Exception($"Failed to install JDK {requiredJava}");
                    }
                }
                else
                {
                    throw new OperationCanceledException($"Java {requiredJava} is required but not installed");
                }
            }
            else
                Debug.WriteLine($"[Gradle] → WARNING: No Java {requiredJava}+ found, using system default");

            // 2) הפעל gradlew
            var args = $"--warning-mode all --stacktrace {gradleTasks}";
            var psi = new ProcessStartInfo(wrapperPath, args)
            {
                WorkingDirectory = projectDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            if (jdkHome != null)
                psi.Environment["JAVA_HOME"] = jdkHome;

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            proc.OutputDataReceived += (s, e) => { if (e.Data != null) Debug.WriteLine($"[Gradle] {e.Data}"); };
            proc.ErrorDataReceived += (s, e) => { if (e.Data != null) Debug.WriteLine($"[Gradle][ERR] {e.Data}"); };

            var tcs = new TaskCompletionSource();
            proc.Exited += (_, _) =>
            {
                if (proc.ExitCode == 0) tcs.SetResult();
                else tcs.SetException(new Exception($"Gradle exited {proc.ExitCode}"));
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await tcs.Task;
        }

        private string? FindBestJdkHome(int requiredVersion = 0)
        {
            // First check: Look for JDK in registry and Program Files
            Version bestV = new(0, 0);
            string? best = null;

            void TryPath(string p)
            {
                if (!Directory.Exists(p)) return;
                if (TryReadReleaseVersion(p, out var v) &&
                    (v.Major >= requiredVersion || requiredVersion == 0))
                {
                    if (v > bestV)
                    {
                        bestV = v;
                        best = p;
                    }
                }
            }

            // Check registry and program files as before
            // [Keep existing registry and program files scanning code]

            // Second check: Look at JAVA_HOME environment variable
            var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome) && Directory.Exists(javaHome))
            {
                Debug.WriteLine($"[JDK Search] Found JAVA_HOME: {javaHome}");
                TryPath(javaHome);
            }

            // Third check: Look for JDK installation directories under JAVA_HOME
            if (!string.IsNullOrEmpty(javaHome))
            {
                var parentDir = Directory.GetParent(javaHome)?.FullName;
                if (parentDir != null && Directory.Exists(parentDir))
                {
                    foreach (var dir in Directory.GetDirectories(parentDir, "jdk*"))
                    {
                        TryPath(dir);
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// קורא את קובץ 'release' ב־JDK home ומחזיר את JAVA_VERSION כ־Version.
        /// </summary>
        private bool TryReadReleaseVersion(string dir, out Version version)
        {
            version = new(0, 0);
            try
            {
                var f = Path.Combine(dir, "release");
                if (!File.Exists(f)) return false;

                foreach (var ln in File.ReadAllLines(f))
                {
                    if (!ln.StartsWith("JAVA_VERSION=")) continue;
                    var parts = ln.Split('"');
                    if (parts.Length < 2) return false;
                    version = Version.Parse(parts[1]);
                    return true;
                }
            }
            catch { /* swallow */ }
            return false;
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